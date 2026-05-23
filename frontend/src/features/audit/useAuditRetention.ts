import { useQuery } from "@tanstack/react-query";
import { api } from "@/lib/api";

export interface AuditRetentionResponse {
  retentionDays: number;
}

export const auditRetentionQueryKey = ["audit", "retention"] as const;

// PR 4.3: retention настраивается оператором редко (раз в месяцы) — 5min staleTime
// минимизирует refetch'и на тривиальные re-render'ы AuditPage.
export function useAuditRetention() {
  return useQuery({
    queryKey: auditRetentionQueryKey,
    queryFn: () => api<AuditRetentionResponse>("/api/v1/audit/retention"),
    staleTime: 5 * 60 * 1000,
  });
}
