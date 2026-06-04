using System.Text.Json;

namespace MitLicenseCenter.Tools.PerfHarness;

// MLC-039 (PERF-03): сценарный файл, который seed-режим пишет, а rac-stub-режим читает.
// Декаплит seed от заглушки: заглушка не лезет в БД, а отдаёт ровно те ClusterInfobaseId,
// что засеяны, плюс S синтетических сессий с нужным распределением over-limit.

internal sealed record PerfScenario(
    Guid ClusterUuid,
    IReadOnlyList<ScenarioSession> Sessions,
    IReadOnlyList<ScenarioInfobase> Infobases);

internal sealed record ScenarioSession(
    Guid SessionId,
    Guid ClusterInfobaseId,
    string AppId,
    string UserName,
    string Host,
    DateTime StartedAtUtc);

internal sealed record ScenarioInfobase(
    Guid Id,
    string Name,
    string? Description);

internal static class ScenarioFile
{
    public const string EnvVar = "MLC_PERF_SCENARIO";

    private static readonly JsonSerializerOptions Json = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    // Дефолт — %LOCALAPPDATA%\MitLicenseCenter\perf\scenario.json (тот же корень, что и
    // dev-ключи DataProtection в AddInfrastructure). Override: env MLC_PERF_SCENARIO или
    // явный --scenario. Backend-процесс наследует env, поэтому заглушка-субпроцесс видит тот
    // же путь, что выставил оператор.
    public static string ResolvePath(string? explicitPath)
    {
        if (!string.IsNullOrWhiteSpace(explicitPath))
        {
            return explicitPath;
        }

        var fromEnv = Environment.GetEnvironmentVariable(EnvVar);
        if (!string.IsNullOrWhiteSpace(fromEnv))
        {
            return fromEnv;
        }

        return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "MitLicenseCenter", "perf", "scenario.json");
    }

    public static async Task SaveAsync(PerfScenario scenario, string path, CancellationToken ct)
    {
        var dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir))
        {
            Directory.CreateDirectory(dir);
        }

        var json = JsonSerializer.Serialize(scenario, Json);
        await File.WriteAllTextAsync(path, json, ct).ConfigureAwait(false);
    }

    // Заглушка вызывается часто и должна быть устойчивой: отсутствие/битость файла → null,
    // вызывающий рендерит «пустой кластер» (ping проходит, сессий нет), а не падает.
    public static PerfScenario? TryLoad(string path)
    {
        try
        {
            if (!File.Exists(path))
            {
                return null;
            }

            var json = File.ReadAllText(path);
            return JsonSerializer.Deserialize<PerfScenario>(json, Json);
        }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
        {
            return null;
        }
    }
}
