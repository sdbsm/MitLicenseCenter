namespace MitLicenseCenter.Web.Endpoints;

// MLC-176 — контракт статуса обновлений. ЗАФИКСИРОВАН: frontend Zod
// (features/updates/types.ts) обязан совпасть. `CurrentVersion` всегда заполнен
// (из Assembly informational version). Когда проверка недоступна
// (CheckAvailable=false: рубильник Updates.Enabled=0 / GitHub вернул null) —
// LatestVersion/ReleaseUrl/DownloadUrl=null и UpdateAvailable=false.
public sealed record UpdateStatusResponse(
    string CurrentVersion,
    string? LatestVersion,
    bool UpdateAvailable,
    string? ReleaseUrl,
    string? DownloadUrl,
    bool CheckAvailable,
    DateTime CheckedAtUtc);
