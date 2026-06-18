import type { Tenant } from "@/features/tenants/types";

/**
 * Строка проекции «По клиентам» (MLC-196a): агрегат потребления лицензий по клиенту.
 * `consumed` — число фактически потребляющих сеансов клиента (склейка с
 * `buildConsumedByTenant`); `limit` — `maxConcurrentLicenses` клиента (0 = безлимит).
 */
export interface ByTenantRow {
  tenantId: string;
  tenantName: string;
  consumed: number;
  limit: number;
}

/**
 * Чистая склейка строк проекции «По клиентам» (MLC-196a) — вынесена ради тестируемости
 * (как `buildConsumedByTenant`/`sortRows`). БЕЗ нового BE-эндпоинта: все клиенты
 * (имя + лимит) приходят из `useAllTenants`, потребление — из `buildConsumedByTenant`
 * над снапшотом сеансов. Клиент без сеансов присутствует с `consumed = 0`.
 *
 * Сортировка: превышения (consumed > limit, лимитированные) — сверху, затем по
 * потреблению ↓; при равном потреблении — по имени клиента (по-русски, стабильно).
 */
export function buildByTenantRows(
  tenants: Tenant[],
  consumedByTenant: Map<string, number>
): ByTenantRow[] {
  const rows: ByTenantRow[] = tenants.map((tenant) => ({
    tenantId: tenant.id,
    tenantName: tenant.name,
    consumed: consumedByTenant.get(tenant.id) ?? 0,
    limit: tenant.maxConcurrentLicenses,
  }));
  return sortByTenantRows(rows);
}

/**
 * Канонический компаратор проекции «По клиентам» (MLC-196a). @internal — отдельно для теста.
 * Превышение = `limit > 0 && consumed > limit` (безлимит превышением быть не может).
 * Превышения идут первыми; внутри групп — по `consumed` убыв., затем имя клиента.
 */
export function sortByTenantRows(rows: ByTenantRow[]): ByTenantRow[] {
  return [...rows].sort((a, b) => {
    const aOver = a.limit > 0 && a.consumed > a.limit;
    const bOver = b.limit > 0 && b.consumed > b.limit;
    if (aOver !== bOver) return aOver ? -1 : 1;
    if (a.consumed !== b.consumed) return b.consumed - a.consumed;
    return a.tenantName.localeCompare(b.tenantName, "ru");
  });
}
