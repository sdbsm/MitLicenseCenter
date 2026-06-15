using System.Globalization;
using System.Text;

namespace MitLicenseCenter.Tools.PerfHarness;

// MLC-039 (PERF-03): режим фейкового rac.exe. SystemProcessRacRunner спавнит этот exe как
// OneC.RAS.ExePath и передаёт реальные rac-аргументы — мы распознаём команду тем же приёмом,
// что тест-BuildRunner, и рендерим вывод в формате RacOutputParser (key : value, пустая строка
// между записями). Весь прод-путь (спавн → парсинг → reconcile/hot/kill) проходит как с живым
// rac.exe, поэтому метрики MLC-037 (rac.exe.spawns, cold/hot.duration, kills) зажигаются.
// Вывод строго ASCII → байты совпадают в CP866/UTF-8/ASCII, OEM-декод в раннере корректен.
internal static class RacStub
{
    internal enum RacCommand
    {
        ClusterList,
        SessionList,
        SessionLicenses,
        SessionTerminate,
        InfobaseSummaryList,
        Unknown,
    }

    internal static RacCommand Classify(IReadOnlyList<string> args)
    {
        var hasSession = Has(args, "session");
        if (hasSession && Has(args, "terminate"))
        {
            return RacCommand.SessionTerminate;
        }
        // ADR-48 (MLC-166): `session list --licenses` — отдельная проекция факта лицензий.
        // Заглушка считает ВСЕ сценарные сессии лицензионными (профиль over-limit строится
        // числом сессий), поэтому возвращает их с блоком license-type — иначе backend
        // классифицирует всех как NotConsuming и kill-поток не зажигается.
        if (hasSession && Has(args, "list") && Has(args, "--licenses"))
        {
            return RacCommand.SessionLicenses;
        }
        if (hasSession && Has(args, "list"))
        {
            return RacCommand.SessionList;
        }
        if (Has(args, "infobase") && Has(args, "summary"))
        {
            return RacCommand.InfobaseSummaryList;
        }
        if (Has(args, "cluster") && Has(args, "list"))
        {
            return RacCommand.ClusterList;
        }
        return RacCommand.Unknown;
    }

    // Возвращает (exitCode, stdout). session terminate — всегда «killed» (exit 0, без вывода):
    // заглушка stateless, тенант остаётся over-limit → устойчивый kill-поток для замера.
    internal static (int ExitCode, string Stdout) Render(RacCommand command, PerfScenario? scenario)
        => command switch
        {
            RacCommand.ClusterList => (0, RenderClusterList(scenario)),
            RacCommand.SessionList => (0, RenderSessionList(scenario)),
            RacCommand.SessionLicenses => (0, RenderSessionLicenses(scenario)),
            RacCommand.InfobaseSummaryList => (0, RenderInfobaseList(scenario)),
            RacCommand.SessionTerminate => (0, string.Empty),
            _ => (0, string.Empty),
        };

    public static int Run(IReadOnlyList<string> args, string scenarioPath, TextWriter stdout)
    {
        var command = Classify(args);
        var scenario = ScenarioFile.TryLoad(scenarioPath);
        var (exitCode, output) = Render(command, scenario);
        if (output.Length > 0)
        {
            stdout.Write(output);
        }
        return exitCode;
    }

    private static string RenderClusterList(PerfScenario? scenario)
    {
        var uuid = scenario?.ClusterUuid ?? Guid.Empty;
        var sb = new StringBuilder();
        sb.Append("cluster : ").Append(uuid.ToString("D")).Append('\n');
        sb.Append("host    : perf-stub\n");
        sb.Append("port    : 1541\n");
        sb.Append("name    : \"Perf stub cluster\"\n");
        return sb.ToString();
    }

    private static string RenderSessionList(PerfScenario? scenario)
    {
        if (scenario is null || scenario.Sessions.Count == 0)
        {
            return string.Empty;
        }

        var sb = new StringBuilder(scenario.Sessions.Count * 200);
        foreach (var s in scenario.Sessions)
        {
            sb.Append("session    : ").Append(s.SessionId.ToString("D")).Append('\n');
            sb.Append("infobase   : ").Append(s.ClusterInfobaseId.ToString("D")).Append('\n');
            sb.Append("app-id     : ").Append(s.AppId).Append('\n');
            sb.Append("user-name  : ").Append(s.UserName).Append('\n');
            sb.Append("host       : ").Append(s.Host).Append('\n');
            sb.Append("started-at : ")
                .Append(s.StartedAtUtc.ToString("yyyy-MM-ddTHH:mm:ss", CultureInfo.InvariantCulture))
                .Append('\n');
            sb.Append('\n');
        }
        return sb.ToString();
    }

    // ADR-48 (MLC-166): проекция `session list --licenses`. Реальный rac опускает поле
    // infobase и добавляет блок лицензии; нелицензионные сессии не попадают в вывод вовсе.
    // Заглушка считает все сценарные сессии лицензионными (HASP), чтобы перф-сценарий
    // over-limit/kill работал так же, как до перехода на факт.
    private static string RenderSessionLicenses(PerfScenario? scenario)
    {
        if (scenario is null || scenario.Sessions.Count == 0)
        {
            return string.Empty;
        }

        var sb = new StringBuilder(scenario.Sessions.Count * 200);
        foreach (var s in scenario.Sessions)
        {
            sb.Append("session          : ").Append(s.SessionId.ToString("D")).Append('\n');
            sb.Append("user-name        : ").Append(s.UserName).Append('\n');
            sb.Append("host             : ").Append(s.Host).Append('\n');
            sb.Append("app-id           : ").Append(s.AppId).Append('\n');
            sb.Append("license-type     : HASP\n");
            sb.Append("short-presentation : ").Append(s.AppId).Append('\n');
            sb.Append('\n');
        }
        return sb.ToString();
    }

    private static string RenderInfobaseList(PerfScenario? scenario)
    {
        if (scenario is null || scenario.Infobases.Count == 0)
        {
            return string.Empty;
        }

        var sb = new StringBuilder(scenario.Infobases.Count * 100);
        foreach (var ib in scenario.Infobases)
        {
            sb.Append("infobase : ").Append(ib.Id.ToString("D")).Append('\n');
            sb.Append("name     : ").Append(ib.Name).Append('\n');
            if (!string.IsNullOrEmpty(ib.Description))
            {
                sb.Append("descr    : ").Append(ib.Description).Append('\n');
            }
            sb.Append('\n');
        }
        return sb.ToString();
    }

    private static bool Has(IReadOnlyList<string> args, string token)
    {
        foreach (var a in args)
        {
            if (string.Equals(a, token, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }
        return false;
    }
}
