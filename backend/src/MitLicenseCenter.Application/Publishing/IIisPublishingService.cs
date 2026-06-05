using MitLicenseCenter.Domain.Publications;

namespace MitLicenseCenter.Application.Publishing;

public interface IIisPublishingService
{
    // Read-only (MLC-045): читает фактическое состояние публикации в IIS — есть ли
    // сайт, виртуальный каталог и web.config, какая версия платформы прописана
    // (путь к wsisapi.dll). НЕ сравнивает с эталоном и ничего не меняет.
    Task<PublicationActualState> ReadActualStateAsync(Publication publication, CancellationToken ct);

    // Смена платформы (MLC-045): правит ТОЛЬКО путь к wsisapi.dll в web.config
    // (fallback — в default.vrd, если путь там) под newVersion. default.vrd
    // содержательно не трогается. Бросает IIS/IO-исключения — эндпоинт ловит → 409.
    Task ChangePlatformAsync(Publication publication, string newVersion, CancellationToken ct);

    // Discovery: список IIS-сайтов. Используется формой публикации вместо ручного
    // ввода SiteName. Может бросить исключение (нет доступа к Metabase / не Windows) —
    // вызывающий эндпоинт ловит и помечает результат как недоступный.
    Task<IReadOnlyList<IisSiteInfo>> ListSitesAsync(CancellationToken ct);
}

public sealed record IisSiteInfo(string SiteName);

// Факт состояния публикации в IIS (MLC-045). PlatformVersion — версия, извлечённая
// из пути к wsisapi.dll (web.config, fallback default.vrd); null, если не найдена.
// Error != null — адаптер не смог прочитать состояние (нет прав / COM / IO).
public sealed record PublicationActualState(
    bool SiteExists,
    bool VirtualPathExists,
    bool WebConfigExists,
    string? PlatformVersion,
    string? Error);
