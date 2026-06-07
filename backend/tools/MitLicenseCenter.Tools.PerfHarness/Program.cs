using System.Globalization;
using MitLicenseCenter.Infrastructure.Identity;

namespace MitLicenseCenter.Tools.PerfHarness;

// MLC-039 (PERF-03) — единый dev/test-only бинарь с режимами:
//   • `PerfHarness seed [--tenants N --infobases M --audit K --sessions S …]` — засев dev-БД
//     + запись scenario.json;
//   • `PerfHarness reset-admin [--user admin] [--password <v>] [--unlock] [--connection <cs>]` —
//     сброс пароля администратора без потери данных через штатный UserManager (MLC-053);
//   • `PerfHarness <rac-аргументы>` — фейковый rac.exe (OneC.RAS.ExePath указывает сюда).
// `seed`/`reset-admin` — наши verb'ы, их rac.exe никогда не передаёт; всё остальное уходит в заглушку.
internal static class Program
{
    private const string DefaultConnectionString =
        "Server=.;Database=MitLicenseCenter;Trusted_Connection=True;TrustServerCertificate=True;Encrypt=False";

    private static async Task<int> Main(string[] args)
    {
        if (args.Length > 0 && string.Equals(args[0], "seed", StringComparison.OrdinalIgnoreCase))
        {
            return await RunSeedAsync(args[1..]).ConfigureAwait(false);
        }

        if (args.Length > 0 && string.Equals(args[0], "reset-admin", StringComparison.OrdinalIgnoreCase))
        {
            return await RunResetAdminAsync(args[1..]).ConfigureAwait(false);
        }

        var scenarioPath = ScenarioFile.ResolvePath(null);
        return RacStub.Run(args, scenarioPath, Console.Out);
    }

    private static async Task<int> RunSeedAsync(string[] args)
    {
        var map = ParseOptions(args);

        // --realistic — флаг без значения (парсер кладёт "true"). В realistic-режиме дефолты
        // сдвигаются: over-limit-доля 0.10 (~10%) и usage-days 365 (год истории под /reports).
        var realistic = map.TryGetValue("realistic", out var rv)
            && !string.Equals(rv, "false", StringComparison.OrdinalIgnoreCase);

        var opts = new SeedOptions
        {
            Tenants = GetInt(map, "tenants", 20),
            Infobases = GetInt(map, "infobases", 50),
            Audit = GetInt(map, "audit", 100_000),
            Sessions = GetInt(map, "sessions", 500),
            OverLimitFraction = GetDouble(map, "over-limit-fraction", realistic ? 0.10 : 0.30),
            Seed = GetInt(map, "seed", 1039),
            AuditDays = GetInt(map, "audit-days", 365),
            UsageDays = GetInt(map, "usage-days", realistic ? 365 : 0),
            Realistic = realistic,
        };

        var connectionString =
            GetString(map, "connection")
            ?? Environment.GetEnvironmentVariable("ConnectionStrings__Default")
            ?? DefaultConnectionString;

        var scenarioPath = ScenarioFile.ResolvePath(GetString(map, "scenario"));

        Console.WriteLine(
            $"PerfHarness seed: realistic={opts.Realistic}, tenants={opts.Tenants}, " +
            $"infobases={opts.Infobases}, audit={opts.Audit} (за {opts.AuditDays}д), " +
            $"sessions={(opts.Realistic ? "из потребления" : opts.Sessions.ToString(CultureInfo.InvariantCulture))}, " +
            $"usage-days={opts.UsageDays}, over-limit={opts.OverLimitFraction:0.##}");

        await Seeder.RunAsync(opts, connectionString, scenarioPath, Console.Out, CancellationToken.None)
            .ConfigureAwait(false);

        Console.WriteLine(
            "Готово. Выставьте OneC.RAS.ExePath на этот PerfHarness.exe (OneC.RAS.Endpoint оставьте " +
            $"пустым) и при необходимости env {ScenarioFile.EnvVar}={scenarioPath}.");
        return 0;
    }

    private static async Task<int> RunResetAdminAsync(string[] args)
    {
        var map = ParseOptions(args);

        var userName = GetString(map, "user") ?? IdentitySeeder.DefaultAdminUserName;
        var password = GetString(map, "password");
        var unlock = map.TryGetValue("unlock", out var u)
            && !string.Equals(u, "false", StringComparison.OrdinalIgnoreCase);

        var connectionString =
            GetString(map, "connection")
            ?? Environment.GetEnvironmentVariable("ConnectionStrings__Default")
            ?? DefaultConnectionString;

        return await AdminReset
            .RunAsync(userName, password, unlock, connectionString, Console.Out, CancellationToken.None)
            .ConfigureAwait(false);
    }

    // Минимальный парсер --key value / --key=value (флаги без значения → "true").
    private static Dictionary<string, string> ParseOptions(string[] args)
    {
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < args.Length; i++)
        {
            var a = args[i];
            if (!a.StartsWith("--", StringComparison.Ordinal))
            {
                continue;
            }

            var body = a[2..];
            var eq = body.IndexOf('=', StringComparison.Ordinal);
            if (eq >= 0)
            {
                map[body[..eq]] = body[(eq + 1)..];
            }
            else if (i + 1 < args.Length && !args[i + 1].StartsWith("--", StringComparison.Ordinal))
            {
                map[body] = args[++i];
            }
            else
            {
                map[body] = "true";
            }
        }
        return map;
    }

    private static string? GetString(Dictionary<string, string> map, string key)
        => map.TryGetValue(key, out var v) && !string.IsNullOrWhiteSpace(v) ? v : null;

    private static int GetInt(Dictionary<string, string> map, string key, int fallback)
        => map.TryGetValue(key, out var v) ? int.Parse(v, CultureInfo.InvariantCulture) : fallback;

    private static double GetDouble(Dictionary<string, string> map, string key, double fallback)
        => map.TryGetValue(key, out var v) ? double.Parse(v, CultureInfo.InvariantCulture) : fallback;
}
