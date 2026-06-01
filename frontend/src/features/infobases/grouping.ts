import type { InfobaseListItem } from "./types";

export interface InfobaseGroup {
  tenantId: string;
  tenantName: string;
  items: InfobaseListItem[];
}

/**
 * Группирует инфобазы по клиенту. Имя клиента берётся из актуальной карты
 * (на случай переименования), с откатом на `tenantName` из самой базы.
 * Группы отсортированы по имени клиента, базы внутри — по имени.
 */
export function groupByTenant(
  items: InfobaseListItem[],
  tenantNameById: Map<string, string>
): InfobaseGroup[] {
  const byTenant = new Map<string, InfobaseListItem[]>();
  for (const item of items) {
    const list = byTenant.get(item.tenantId);
    if (list) list.push(item);
    else byTenant.set(item.tenantId, [item]);
  }
  return [...byTenant.entries()]
    .map(([tenantId, groupItems]) => ({
      tenantId,
      tenantName: tenantNameById.get(tenantId) ?? groupItems[0]?.tenantName ?? tenantId,
      items: [...groupItems].sort((a, b) => a.name.localeCompare(b.name, "ru")),
    }))
    .sort((a, b) => a.tenantName.localeCompare(b.tenantName, "ru"));
}
