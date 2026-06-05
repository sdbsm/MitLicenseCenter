using System.Diagnostics;
using System.Globalization;
using System.Runtime.Versioning;
using System.ServiceProcess;
using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.Web.Administration;
using MitLicenseCenter.Application.Publishing;

namespace MitLicenseCenter.Infrastructure.Publishing;

// Реальный адаптер управления жизненным циклом IIS (MLC-047, ADR-24). Две группы
// операций:
//   1) Пул/сайт через ServerManager (Microsoft.Web.Administration): recycle/start/stop
//      пула, start/stop/restart сайта, discovery с состоянием. Это runtime-команды —
//      CommitChanges() не нужен (в отличие от правки конфигурации).
//   2) Полный перезапуск через спавн iisreset.exe (роняет ВСЕ сайты сервера).
//
// Все мутации сериализованы через IIisResetConcurrencyGate (N=1) — два recycle/iisreset
// одновременно недопустимы. Discovery замок не берёт.
//
// Windows-only: ServerManager и iisreset доступны только на Windows. Тесты эндпоинтов
// идут через StubIisLifecycleService / мок IIisLifecycleService.
[SupportedOSPlatform("windows")]
internal sealed partial class OneCIisLifecycleService : IIisLifecycleService
{
    // iisreset останавливает и стартует W3SVC/WAS со всеми сайтами — на нагруженном
    // сервере дольше точечного webinst (60s). 90s — щедрый потолок.
    private static readonly TimeSpan IisResetTimeout = TimeSpan.FromSeconds(90);

    // iisreset.exe — системная утилита: пишет в OEM-кодировку (CP866 на RU Windows),
    // как rac.exe (ADR-3.3), НЕ UTF-16 (это особенность webinst). См. ResolveOemEncoding.
    private static readonly Encoding OemEncoding = ResolveOemEncoding();

    private readonly IIisResetConcurrencyGate _gate;
    private readonly ILogger<OneCIisLifecycleService> _logger;

    public OneCIisLifecycleService(IIisResetConcurrencyGate gate, ILogger<OneCIisLifecycleService> logger)
    {
        _gate = gate;
        _logger = logger;
    }

    public Task<IReadOnlyList<IisAppPoolInfo>> ListApplicationPoolsAsync(CancellationToken ct)
    {
        // Исключения (нет доступа к Metabase / COM) пробрасываются — эндпоинт ловит
        // и помечает результат недоступным (Available:false), как discovery-сайтов.
        using var sm = new ServerManager();
        var pools = sm.ApplicationPools
            .Select(p => new IisAppPoolInfo(p.Name, MapState(p.State)))
            .ToList();
        return Task.FromResult<IReadOnlyList<IisAppPoolInfo>>(pools);
    }

    public Task<IReadOnlyList<IisSiteStateInfo>> ListSitesAsync(CancellationToken ct)
    {
        using var sm = new ServerManager();
        var sites = sm.Sites
            .Select(s => new IisSiteStateInfo(s.Name, MapState(s.State)))
            .ToList();
        return Task.FromResult<IReadOnlyList<IisSiteStateInfo>>(sites);
    }

    public Task<IisObjectState> GetServerStateAsync(CancellationToken ct)
    {
        // Статус службы W3SVC через SCM — надёжен даже когда IIS остановлен (в отличие
        // от ServerManager). Бросает (служба не найдена / нет прав) — эндпоинт ловит.
        using var sc = new ServiceController("W3SVC");
        return Task.FromResult(MapServiceState(sc.Status));
    }

    public async Task<IisObjectState> RecycleApplicationPoolAsync(string poolName, CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(poolName);
        using (await _gate.AcquireAsync(ct).ConfigureAwait(false))
        {
            using var sm = new ServerManager();
            var pool = RequirePool(sm, poolName);
            pool.Recycle();
            LogPoolRecycled(_logger, poolName);
            return MapState(pool.State);
        }
    }

    public async Task<IisObjectState> StartApplicationPoolAsync(string poolName, CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(poolName);
        using (await _gate.AcquireAsync(ct).ConfigureAwait(false))
        {
            using var sm = new ServerManager();
            var pool = RequirePool(sm, poolName);
            // Идемпотентно: повторный Start уже запущенного пула — no-op (иначе COM-ошибка).
            if (pool.State is not (ObjectState.Started or ObjectState.Starting))
            {
                pool.Start();
            }
            LogPoolStarted(_logger, poolName);
            return MapState(pool.State);
        }
    }

    public async Task<IisObjectState> StopApplicationPoolAsync(string poolName, CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(poolName);
        using (await _gate.AcquireAsync(ct).ConfigureAwait(false))
        {
            using var sm = new ServerManager();
            var pool = RequirePool(sm, poolName);
            if (pool.State is not (ObjectState.Stopped or ObjectState.Stopping))
            {
                pool.Stop();
            }
            LogPoolStopped(_logger, poolName);
            return MapState(pool.State);
        }
    }

    public async Task<IisObjectState> StartSiteAsync(string siteName, CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(siteName);
        using (await _gate.AcquireAsync(ct).ConfigureAwait(false))
        {
            using var sm = new ServerManager();
            var site = RequireSite(sm, siteName);
            if (site.State is not (ObjectState.Started or ObjectState.Starting))
            {
                site.Start();
            }
            LogSiteStarted(_logger, siteName);
            return MapState(site.State);
        }
    }

    public async Task<IisObjectState> StopSiteAsync(string siteName, CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(siteName);
        using (await _gate.AcquireAsync(ct).ConfigureAwait(false))
        {
            using var sm = new ServerManager();
            var site = RequireSite(sm, siteName);
            if (site.State is not (ObjectState.Stopped or ObjectState.Stopping))
            {
                site.Stop();
            }
            LogSiteStopped(_logger, siteName);
            return MapState(site.State);
        }
    }

    public async Task<IisObjectState> RestartSiteAsync(string siteName, CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(siteName);
        using (await _gate.AcquireAsync(ct).ConfigureAwait(false))
        {
            using var sm = new ServerManager();
            var site = RequireSite(sm, siteName);
            // Stop+Start атомарно за границей. Stop пропускаем, если уже остановлен.
            if (site.State is not (ObjectState.Stopped or ObjectState.Stopping))
            {
                site.Stop();
            }
            site.Start();
            LogSiteRestarted(_logger, siteName);
            return MapState(site.State);
        }
    }

    public Task RestartIisAsync(CancellationToken ct) => RunIisResetGatedAsync([], "restart", ct);

    public Task StopIisAsync(CancellationToken ct) => RunIisResetGatedAsync(["/stop"], "stop", ct);

    public Task StartIisAsync(CancellationToken ct) => RunIisResetGatedAsync(["/start"], "start", ct);

    private async Task RunIisResetGatedAsync(string[] args, string op, CancellationToken ct)
    {
        using (await _gate.AcquireAsync(ct).ConfigureAwait(false))
        {
            var exePath = Path.Combine(Environment.SystemDirectory, "iisreset.exe");
            var (exitCode, stdout, stderr) = await RunIisResetAsync(exePath, args, ct).ConfigureAwait(false);
            if (exitCode == 0)
            {
                LogIisServerOp(_logger, op);
                return;
            }

            // Полный вывод iisreset (русский, OEM-декодированный) — в журнал сервера;
            // наружу — общий санитизированный текст (MLC-009).
            LogIisServerOpFailed(_logger, op, exitCode, $"{stdout}\n{stderr}".Trim());
            throw new InvalidOperationException(
                "Не удалось выполнить операцию iisreset. Подробности — в журнале сервера.");
        }
    }

    private static ApplicationPool RequirePool(ServerManager sm, string poolName) =>
        sm.ApplicationPools[poolName]
        ?? throw new KeyNotFoundException($"Пул приложений «{poolName}» не найден в IIS.");

    private static Site RequireSite(ServerManager sm, string siteName) =>
        sm.Sites[siteName]
        ?? throw new KeyNotFoundException($"Сайт IIS «{siteName}» не найден.");

    private static IisObjectState MapState(ObjectState state) => state switch
    {
        ObjectState.Starting => IisObjectState.Starting,
        ObjectState.Started => IisObjectState.Started,
        ObjectState.Stopping => IisObjectState.Stopping,
        ObjectState.Stopped => IisObjectState.Stopped,
        _ => IisObjectState.Unknown,
    };

    private static IisObjectState MapServiceState(ServiceControllerStatus status) => status switch
    {
        ServiceControllerStatus.Running => IisObjectState.Started,
        ServiceControllerStatus.StartPending or ServiceControllerStatus.ContinuePending => IisObjectState.Starting,
        ServiceControllerStatus.StopPending or ServiceControllerStatus.PausePending => IisObjectState.Stopping,
        ServiceControllerStatus.Stopped or ServiceControllerStatus.Paused => IisObjectState.Stopped,
        _ => IisObjectState.Unknown,
    };

    // Спавн iisreset.exe по образцу OneCWebinstPublisher.RunAsync, но с OEM-декодом
    // (системная утилита) и таймаутом 90s. args: [] = restart, [/stop], [/start].
    private static async Task<(int ExitCode, string Stdout, string Stderr)> RunIisResetAsync(
        string exePath,
        string[] args,
        CancellationToken ct)
    {
        var psi = new ProcessStartInfo
        {
            FileName = exePath,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
        };
        foreach (var arg in args)
            psi.ArgumentList.Add(arg);

        using var process = new Process { StartInfo = psi };
        process.Start();

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeoutCts.CancelAfter(IisResetTimeout);

        await using var registration = timeoutCts.Token.Register(static state =>
        {
            try
            {
                var p = (Process)state!;
                if (!p.HasExited)
                    p.Kill(entireProcessTree: true);
            }
            catch
            {
                // Процесс уже завершился — нормальная гонка WaitForExit/Kill.
            }
        }, process);

        using var stdoutBuf = new MemoryStream();
        using var stderrBuf = new MemoryStream();
        var stdoutTask = process.StandardOutput.BaseStream.CopyToAsync(stdoutBuf, timeoutCts.Token);
        var stderrTask = process.StandardError.BaseStream.CopyToAsync(stderrBuf, timeoutCts.Token);

        try
        {
            await Task.WhenAll(stdoutTask, stderrTask, process.WaitForExitAsync(timeoutCts.Token))
                .ConfigureAwait(false);
            return (
                process.ExitCode,
                OemEncoding.GetString(stdoutBuf.ToArray()),
                OemEncoding.GetString(stderrBuf.ToArray()));
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            // Локальный таймаут — синтетический non-zero exit, как у rac/webinst-раннеров.
            return (-1, OemEncoding.GetString(stdoutBuf.ToArray()),
                $"iisreset.exe не уложился в таймаут {IisResetTimeout.TotalSeconds:0}s.");
        }
    }

    private static Encoding ResolveOemEncoding()
    {
        // .NET Core+ требует регистрации code-page providers для CP866/CP1251/etc.
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        try
        {
            var oemCp = CultureInfo.CurrentCulture.TextInfo.OEMCodePage;
            if (oemCp > 0)
            {
                return Encoding.GetEncoding(oemCp);
            }
        }
        catch (ArgumentException)
        {
            // Неизвестная кодовая страница → UTF-8 как разумный дефолт.
        }
        return Encoding.UTF8;
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "IIS: пул «{Pool}» переработан (recycle)")]
    private static partial void LogPoolRecycled(ILogger logger, string pool);

    [LoggerMessage(Level = LogLevel.Information, Message = "IIS: пул «{Pool}» запущен")]
    private static partial void LogPoolStarted(ILogger logger, string pool);

    [LoggerMessage(Level = LogLevel.Information, Message = "IIS: пул «{Pool}» остановлен")]
    private static partial void LogPoolStopped(ILogger logger, string pool);

    [LoggerMessage(Level = LogLevel.Information, Message = "IIS: сайт «{Site}» запущен")]
    private static partial void LogSiteStarted(ILogger logger, string site);

    [LoggerMessage(Level = LogLevel.Information, Message = "IIS: сайт «{Site}» остановлен")]
    private static partial void LogSiteStopped(ILogger logger, string site);

    [LoggerMessage(Level = LogLevel.Information, Message = "IIS: сайт «{Site}» перезапущен")]
    private static partial void LogSiteRestarted(ILogger logger, string site);

    [LoggerMessage(Level = LogLevel.Information, Message = "IIS: операция iisreset «{Op}» выполнена")]
    private static partial void LogIisServerOp(ILogger logger, string op);

    [LoggerMessage(Level = LogLevel.Warning, Message = "iisreset «{Op}»: exit={ExitCode}. Вывод: {Output}")]
    private static partial void LogIisServerOpFailed(ILogger logger, string op, int exitCode, string output);
}
