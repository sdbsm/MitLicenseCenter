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

    // ── Игнор-лист «нераспределённых» баз кластера (MLC-092) ───────────────────────
    public static string UnassignedInfobaseHidden(string name, string initiator) =>
        $"База кластера «{name}» скрыта из списка нераспределённых администратором {initiator}.";

    public static string UnassignedInfobaseUnhidden(string name, string initiator) =>
        $"База кластера «{name}» возвращена в список нераспределённых администратором {initiator}.";

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

    // ── Управление учётками пользователей (MLC-058; раздел переименован в MLC-060) ──
    // Пароль в описание НЕ включаем — он возвращается в ответе API и показывается в UI
    // один раз; аудит фиксирует только факт действия и кто кого затронул. «администратором
    // {initiator}» — про роль исполнителя действия (эндпоинты требуют Admin), не про раздел.
    public static string UserCreated(string userName, string role, string initiator) =>
        $"Учётная запись «{userName}» (роль {role}) создана администратором {initiator}.";

    public static string UserDisabled(string userName, string initiator) =>
        $"Учётная запись «{userName}» отключена администратором {initiator}.";

    public static string UserEnabled(string userName, string initiator) =>
        $"Учётная запись «{userName}» включена администратором {initiator}.";

    public static string UserPasswordReset(string userName, string initiator) =>
        $"Пароль учётной записи «{userName}» сброшен администратором {initiator}.";

    // MLC-061 — смена роли существующей учётки. Указываем старую и новую роль.
    public static string UserRoleChanged(string userName, string oldRole, string newRole, string initiator) =>
        $"Роль учётной записи «{userName}» изменена с {oldRole} на {newRole} администратором {initiator}.";

    // ── Бэкапы баз SQL (MLC-077, ADR-27) ────────────────────────────────────────────
    // Здесь — только действия, которые пишет Web (запрос = Viewer-оператор, удаление =
    // Admin). Итоги выполнения (BackupSucceeded/BackupFailed) и ночную очистку
    // (BackupsPurged) пишут оркестратор и TTL-джоба в Infrastructure — Web-каталог
    // им недоступен (направление слоёв), формулировки живут рядом с их кодом.
    public static string BackupRequested(string databaseName, string initiator) =>
        $"Бэкап базы «{databaseName}» поставлен в очередь оператором {initiator}.";

    public static string BackupDeleted(string databaseName, string initiator) =>
        $"Бэкап базы «{databaseName}» удалён администратором {initiator}.";
}
