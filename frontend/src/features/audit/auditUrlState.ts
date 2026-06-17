import {
  AUDIT_ACTION_TYPES,
  AUDIT_PAGE_SIZES,
  type AuditActionType,
  type AuditFilters,
  type AuditPageSize,
  DEFAULT_AUDIT_PAGE_SIZE,
} from "./types";

export function parseFiltersFromUrl(params: URLSearchParams): AuditFilters {
  // Любая невалидная пара ключ-значение в URL → дефолт. URL остаётся
  // shareable-link'ом, но битый параметр не должен ронять страницу.
  const actionRaw = params.get("actionType");
  const action: AuditActionType | null =
    actionRaw && (AUDIT_ACTION_TYPES as readonly string[]).includes(actionRaw)
      ? (actionRaw as AuditActionType)
      : null;

  const pageRaw = Number(params.get("page"));
  const page = Number.isFinite(pageRaw) && pageRaw > 0 ? Math.floor(pageRaw) : 1;

  const sizeRaw = Number(params.get("pageSize"));
  const pageSize: AuditPageSize = (AUDIT_PAGE_SIZES as readonly number[]).includes(sizeRaw)
    ? (sizeRaw as AuditPageSize)
    : DEFAULT_AUDIT_PAGE_SIZE;

  return {
    actionType: action,
    tenantId: params.get("tenantId"),
    from: params.get("from"),
    to: params.get("to"),
    search: params.get("search"),
    page,
    pageSize,
  };
}

export function filtersToUrl(filters: AuditFilters): URLSearchParams {
  const params = new URLSearchParams();
  if (filters.actionType) params.set("actionType", filters.actionType);
  if (filters.tenantId) params.set("tenantId", filters.tenantId);
  if (filters.from) params.set("from", filters.from);
  if (filters.to) params.set("to", filters.to);
  if (filters.search) params.set("search", filters.search);
  if (filters.page !== 1) params.set("page", String(filters.page));
  if (filters.pageSize !== DEFAULT_AUDIT_PAGE_SIZE) {
    params.set("pageSize", String(filters.pageSize));
  }
  return params;
}

// Backend отдаёт ISO UTC; для фильтра нужен RFC3339 с временем (00:00 / 23:59).
// `<input type="date">` хранит «YYYY-MM-DD», превращаем в полноценный ISO.
function dateOnlyToIsoStart(date: string): string {
  return `${date}T00:00:00Z`;
}
function dateOnlyToIsoEnd(date: string): string {
  return `${date}T23:59:59Z`;
}

export function buildBackendFilters(ui: AuditFilters): AuditFilters {
  return {
    ...ui,
    from: ui.from ? dateOnlyToIsoStart(ui.from) : null,
    to: ui.to ? dateOnlyToIsoEnd(ui.to) : null,
  };
}
