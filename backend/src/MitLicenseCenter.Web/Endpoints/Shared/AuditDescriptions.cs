using MitLicenseCenter.Domain.Publications;

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

    // Reconcile исторически без « » вокруг метки и со словом «оператором» — сохраняем буквально.
    public static string PublicationReconciled(
        string label,
        PublicationDriftStatus previousStatus,
        PublicationDriftStatus newStatus,
        string initiator) =>
        $"Публикация {label} согласована оператором {initiator}: статус {previousStatus} → {newStatus}.";
}
