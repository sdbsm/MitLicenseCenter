import { useQuery } from "@tanstack/react-query";
import { api } from "@/lib/api";
import type { AuditFilters, AuditPagedResponse } from "./types";

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
  return qs.toString();
}

export function useAuditLog(filters: AuditFilters) {
  return useQuery({
    // Сериализованные фильтры → стабильный ключ кэша, который меняется только при
    // изменении самих фильтров (URL-state синхронизирован через useSearchParams).
    queryKey: [...auditQueryKey, filters],
    queryFn: () => api<AuditPagedResponse>(`/api/v1/audit?${buildQuery(filters)}`),
    placeholderData: (prev) => prev,
  });
}
