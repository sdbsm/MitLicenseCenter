using MitLicenseCenter.Domain.Common;

namespace MitLicenseCenter.Domain.Publications;

public sealed class Publication : IEntity
{
    public Guid Id { get; init; }
    public Guid InfobaseId { get; init; }
    public required string SiteName { get; set; }
    public required string VirtualPath { get; set; }
    public required string PlatformVersion { get; set; }
    public DateTime CreatedAt { get; init; }
    public DateTime? UpdatedAt { get; set; }

    // Происхождение публикации (MLC-045). Webinst — публикация создана/перезаписана
    // через панель (webinst.exe). Configurator — отмечена как ручная (конфигуратор/
    // внешний webinst). Unknown — ещё не известно (дефолт для строк, существовавших
    // до MLC-045). Гейтит безопасность повторной webinst-публикации: молча
    // перезатирать можно только свои (Webinst).
    public PublicationSource Source { get; set; }

    // Read-only статус публикации (MLC-045): результат последней проверки факта в IIS
    // (есть ли сайт/vdir/web.config, какая версия платформы). Заполняется проверкой
    // («Проверить сейчас» + фоновый refresh). НЕ enforcement — сравнения с эталоном
    // и авто-исправления больше нет (ADR-4 переписан, ADR-4.1 revoked).
    public PublicationPublishStatus LastCheckStatus { get; set; }
    public DateTime? LastCheckAt { get; set; }
    public string? LastCheckDetails { get; set; }

    // Physical-path override (PR 4.1). Если задан — resolver использует этот путь
    // к папке IIS-приложения вместо convention {IIS.DefaultVrdRoot}/{siteName}/{virtualPath}.
    // NULL/empty → fallback на convention (нет migration noise для существующих строк).
    public string? PhysicalPathOverride { get; set; }
}
