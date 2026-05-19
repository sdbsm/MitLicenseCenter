namespace MitLicenseCenter.Application.Settings;

// Хранилище runtime-параметров. Plain-значения уезжают/приходят как есть,
// секреты шифруются DPAPI внутри реализации — слой Application про шифрование
// не знает. Возврат `null` из Get'ов значит «параметр не задан», для
// hot-path-адаптеров достаточно ISettingsSnapshot (см. рядом) с in-mem TTL.
public interface ISettingsStore
{
    Task<string?> GetAsync(string key, CancellationToken ct = default);
    Task<int?> GetIntAsync(string key, CancellationToken ct = default);
    Task SetAsync(string key, string? value, bool isSecret, string updatedBy, CancellationToken ct = default);
    Task<IReadOnlyList<SettingDescriptor>> ListAsync(CancellationToken ct = default);
}

// `IsSet=false` — отдельный сигнал «значение никогда не выставлялось» (после
// seed'а), `ValueText` маскируется до null для секретов даже когда оно
// технически отсутствует.
public sealed record SettingDescriptor(
    string Key,
    bool IsSecret,
    bool IsSet,
    string? ValueText,
    string? Description,
    DateTime UpdatedAt,
    string UpdatedBy);
