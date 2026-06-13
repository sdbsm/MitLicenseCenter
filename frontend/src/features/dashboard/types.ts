import { z } from "zod";
import { omittable } from "@/lib/apiSchema";

// Stage 5 PR 5.1 (ADR-16): REST cluster adapter + Polly circuit breaker удалены.
// Dashboard cluster card → RAS health card; backend publish'ит снапшот через
// IRasHealthReader + RasHealthProbingService (30s ping cadence).
//
// FE-19 (MLC-120): сводка дашборда проходит толерантную Zod-границу (по образцу
// backupSummarySchema). Раньше `useDashboardSummary` кастил ответ через `api<T>()` без
// рантайм-проверки. Схема ТОЛЕРАНТНА: незнакомые доп.поля будущего бэкенда не роняют
// парс (z.object по умолчанию игнорирует лишние ключи), required числа остаются required.
//
// **Бэкенд опускает null-поля** (`JsonIgnoreCondition.WhenWritingNull`, урок
// [[api-omits-null-fields]], MLC-067/071): у RAS-health `lastCheckedAtUtc`/
// `lastErrorMessage` при null НЕ приходят как `null`, а отсутствуют — поэтому оба через
// `omittable()` (`.nullable()` уронил бы границу при первом ping'е до первой проверки).
export const dashboardRasHealthSchema = z.object({
  healthy: z.boolean(),
  lastCheckedAtUtc: omittable(z.string()),
  lastErrorMessage: omittable(z.string()),
  consecutiveFailures: z.number(),
});

export const tenantConsumptionRowSchema = z.object({
  tenantId: z.string(),
  tenantName: z.string(),
  consumed: z.number(),
  limit: z.number(),
  percent: z.number(),
});

export const dashboardSummarySchema = z.object({
  tenantsTotal: z.number(),
  tenantsActive: z.number(),
  infobasesTotal: z.number(),
  sessionsActiveTotal: z.number(),
  licensesConsumedTotal: z.number(),
  licensesAvailableTotal: z.number(),
  topTenantsByConsumption: z.array(tenantConsumptionRowSchema),
  ras: dashboardRasHealthSchema,
});

export type DashboardRasHealth = z.infer<typeof dashboardRasHealthSchema>;
export type TenantConsumptionRow = z.infer<typeof tenantConsumptionRowSchema>;
export type DashboardSummaryResponse = z.infer<typeof dashboardSummarySchema>;
