import { z } from "zod";
import { useQuery } from "@tanstack/react-query";
import { api } from "@/lib/api";

// Зеркало AuditRetentionResponse (AuditContracts.cs). Все поля non-null — бэкенд
// не опускает их (retentionDays всегда присутствует). Простая схема без omittable.
export const auditRetentionResponseSchema = z.object({
  retentionDays: z.number(),
});

export type AuditRetentionResponse = z.infer<typeof auditRetentionResponseSchema>;

export const auditRetentionQueryKey = ["audit", "retention"] as const;

// PR 4.3: retention настраивается оператором редко (раз в месяцы) — 5min staleTime
// минимизирует refetch'и на тривиальные re-render'ы AuditPage.
// MLC-132: ответ проходит Zod-валидацию (auditRetentionResponseSchema).
export function useAuditRetention() {
  return useQuery({
    queryKey: auditRetentionQueryKey,
    queryFn: () =>
      api<AuditRetentionResponse>("/api/v1/audit/retention", {
        schema: auditRetentionResponseSchema,
      }),
    staleTime: 5 * 60 * 1000,
  });
}
