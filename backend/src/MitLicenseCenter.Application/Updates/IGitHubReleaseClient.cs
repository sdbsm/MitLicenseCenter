namespace MitLicenseCenter.Application.Updates;

// MLC-176 — анти-коррупционный порт к GitHub Releases API. Единственный внешний
// HTTP-источник панели; реализация (Infrastructure) изолирует контракт GitHub от
// домена. Контракт намеренно «толерантен к отказу»: всё, что мешает узнать
// последний релиз (нет сети, HTTP-не-2xx, rate-limit 403, битый JSON), ловится
// ВНУТРИ реализации и наружу отдаётся как `null` = «проверка недоступна».
// Клиентскую отмену (ct) реализация пробрасывает через OperationCanceledException —
// это не «проверка недоступна», а прерванный запрос.
public interface IGitHubReleaseClient
{
    Task<LatestReleaseInfo?> GetLatestReleaseAsync(string ownerRepo, CancellationToken ct);
}

// `InstallerDownloadUrl` = browser_download_url первого ассета с именем,
// оканчивающимся на `.exe` (case-insensitive); нет такого ассета → null
// (баннер покажет ссылку «Открыть релиз», но не кнопку «Скачать установщик»).
public sealed record LatestReleaseInfo(string TagName, string HtmlUrl, string? InstallerDownloadUrl);
