import { useQuery } from "@tanstack/react-query";
import { api } from "@/lib/api";
import { auditPagedResponseSchema, type AuditFilters, type AuditPagedResponse } from "./types";

export const auditQueryKey = ["audit"] as const;

function buildQuery(filters: AuditFilters): string {
  const qs = new URLSearchParams();
  qs.set("page", String(filters.page));
  qs.set("pageSize", String(filters.pageSize));
  if (filters.actionType) {
    qs.set("actionType", filters.actionType);
  }
  if (filters.tenantId) {
    qs.set("tenantId", filters.tenantId);
  }
  if (filters.from) {
    qs.set("from", filters.from);
  }
  if (filters.to) {
    qs.set("to", filters.to);
  }
  if (filters.search) {
    qs.set("search", filters.search);
  }
  return qs.toString();
}

// MLC-132: ответ проходит Zod-валидацию (auditPagedResponseSchema).
// reason/tenantId опускаются бэкендом при null (WhenWritingNull) → omittable().
// actionType forward-compatible: незнакомое будущее значение пропускается как сырая строка.
export function useAuditLog(filters: AuditFilters) {
  return useQuery({
    queryKey: [...auditQueryKey, filters],
    queryFn: () =>
      api<AuditPagedResponse>(`/api/v1/audit?${buildQuery(filters)}`, {
        schema: auditPagedResponseSchema,
      }),
    placeholderData: (prev) => prev,
  });
}
