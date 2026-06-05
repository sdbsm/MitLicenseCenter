namespace MitLicenseCenter.Web.Endpoints;

// MLC-021 — единый каталог русских формулировок аудита для мутирующих эндпоинтов.
// Раньше строки собирались инлайн в каждом обработчике (дрейф формулировок, нельзя
// протестировать как единицу). Тексты перенесены дословно — поведение 1:1.
//
// Метка публикации (`label`) — это SiteName+VirtualPath; пунктуацию вокруг неё
// (« » или без) задаёт сам шаблон, поэтому намеренно две разные формулировки для
// одного AuditActionType, где они исторически различались.
internal static class AuditDescriptions
{
    // ── Клиенты (Tenant) ──────────────────────────────────────────────────────────
    public static string TenantCreated(string name, string initiator) =>
        $"Клиент «{name}» создан администратором {initiator}.";

    public static string TenantUpdated(string name, string initiator) =>
        $"Клиент «{name}» обновлён администратором {initiator}.";

    public static string TenantDeleted(string name, string initiator) =>
        $"Клиент «{name}» удалён администратором {initiator}.";

    // ── Инфобазы (Infobase) ───────────────────────────────────────────────────────
    public static string InfobaseCreated(string name, string initiator) =>
        $"Инфобаза «{name}» создана администратором {initiator}.";

    public static string InfobaseUpdated(string name, string initiator) =>
        $"Инфобаза «{name}» обновлена администратором {initiator}.";

    public static string InfobaseDeleted(string name, string initiator) =>
        $"Инфобаза «{name}» удалена администратором {initiator}.";

    public static string InfobaseReassigned(string name, string sourceName, string targetName, string initiator) =>
        $"Инфобаза «{name}» перенесена от клиента «{sourceName}» к клиенту «{targetName}» администратором {initiator}.";

    // ── Публикации в составе агрегата Infobase ─────────────────────────────────────
    public static string PublicationCreatedForInfobase(string label, string infobaseName, string initiator) =>
        $"Публикация «{label}» создана для инфобазы «{infobaseName}» администратором {initiator}.";

    public static string PublicationUpdatedForInfobase(string label, string infobaseName, string initiator) =>
        $"Публикация «{label}» обновлена для инфобазы «{infobaseName}» администратором {initiator}.";

    public static string PublicationDeletedWithInfobase(string label, string infobaseName, string initiator) =>
        $"Публикация «{label}» удалена вместе с инфобазой «{infobaseName}» администратором {initiator}.";

    // ── Прямое редактирование публикации (PublicationsEndpoints) ───────────────────
    public static string PublicationUpdated(string label, string initiator) =>
        $"Публикация «{label}» обновлена администратором {initiator}.";

    // ── webinst-публикация и смена платформы (MLC-045) ─────────────────────────────
    public static string PublicationPublished(string label, string initiator) =>
        $"Публикация «{label}» опубликована через webinst администратором {initiator}.";

    public static string PublicationPlatformChanged(
        string label,
        string previousVersion,
        string newVersion,
        string initiator) =>
        $"Платформа публикации «{label}» изменена с {previousVersion} на {newVersion} администратором {initiator}.";

    // ── Управление жизненным циклом IIS (MLC-047) ──────────────────────────────────
    public static string IisApplicationPoolRecycled(string pool, string initiator) =>
        $"Пул приложений «{pool}» переработан (recycle) администратором {initiator}.";

    public static string IisApplicationPoolStarted(string pool, string initiator) =>
        $"Пул приложений «{pool}» запущен администратором {initiator}.";

    public static string IisApplicationPoolStopped(string pool, string initiator) =>
        $"Пул приложений «{pool}» остановлен администратором {initiator}.";

    public static string IisSiteStarted(string site, string initiator) =>
        $"Сайт IIS «{site}» запущен администратором {initiator}.";

    public static string IisSiteStopped(string site, string initiator) =>
        $"Сайт IIS «{site}» остановлен администратором {initiator}.";

    public static string IisSiteRestarted(string site, string initiator) =>
        $"Сайт IIS «{site}» перезапущен администратором {initiator}.";

    public static string IisReset(string initiator) =>
        $"Выполнен полный перезапуск IIS (iisreset) администратором {initiator}.";

    public static string IisStopped(string initiator) =>
        $"Выполнена полная остановка IIS (iisreset /stop) администратором {initiator}.";

    public static string IisStarted(string initiator) =>
        $"Выполнен полный запуск IIS (iisreset /start) администратором {initiator}.";
}
