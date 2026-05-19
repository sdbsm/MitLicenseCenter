namespace MitLicenseCenter.Application.Settings;

// Hot-path readers (PR 3.2/3.3 адаптеры/jobs) дёргают snapshot, а не БД на
// каждый tick. Реализация — singleton с TTL ≈ 30s, мутация через
// ISettingsStore.SetAsync вызывает Invalidate() явно.
public interface ISettingsSnapshot
{
    string? GetString(string key);
    int? GetInt(string key);
    void Invalidate();
}
