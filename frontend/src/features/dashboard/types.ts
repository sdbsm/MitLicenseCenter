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
  // ADR-48 (MLC-166): false ⇒ факт rac --licenses недоступен → баннер у потребления.
  // non-nullable bool на бэкенде; .default страхует parity при отсутствии ключа.
  licenseFactAvailable: z.boolean().default(false),
  // MLC-198 — поле БОЛЬШЕ НЕ рендерится на FE: блок «Топ-клиенты по нагрузке» убран с
  // «Обзора» (дублировал «Сеансы → По клиентам», MLC-196a). Контракт /dashboard/summary
  // не меняем (BE не трогаем, parity цела) — поле остаётся в схеме толерантным; кандидат
  // на BE-чистку отдельной задачей.
  topTenantsByConsumption: z.array(tenantConsumptionRowSchema),
  ras: dashboardRasHealthSchema,
});

export type DashboardRasHealth = z.infer<typeof dashboardRasHealthSchema>;
export type TenantConsumptionRow = z.infer<typeof tenantConsumptionRowSchema>;
export type DashboardSummaryResponse = z.infer<typeof dashboardSummarySchema>;

// MLC-186a — серверный агрегат сигналов «Требует внимания» (/dashboard/alerts). Отдельная
// толерантная граница (как summary). Бэкенд опускает null-поля ([[api-omits-null-fields]]):
// `clusterDrift` отсутствует для не-Admin (дрейф — Admin-only), `freeBytes`/счётчики дрейфа
// отсутствуют в degraded — поэтому через `omittable()` (нормализует отсутствие/null → null).
export const dashboardClusterDriftSchema = z.object({
  available: z.boolean(),
  unassignedBases: omittable(z.number()),
  basesNotInCluster: omittable(z.number()),
});

export const dashboardBackupDiskSchema = z.object({
  configured: z.boolean(),
  freeBytes: omittable(z.number()),
  safetyMarginBytes: z.number(),
  low: z.boolean(),
});

// MLC-193 — три ФАКТИЧЕСКИХ бакета квоты лицензий (зеркало backend QuotaBucket / lib/quota.ts
// quotaLabel), а НЕ severity-цвет: превышение = consumed > limit; лимит достигнут = consumed ==
// limit; близко к лимиту = ниже лимита, но процент ≥ warning-порога (75 %).
export const dashboardAlertsSchema = z.object({
  quotaExceeded: z.number(),
  quotaAtLimit: z.number(),
  quotaNearLimit: z.number(),
  clusterDrift: omittable(dashboardClusterDriftSchema),
  backupDisk: dashboardBackupDiskSchema,
});

export type DashboardClusterDriftAlert = z.infer<typeof dashboardClusterDriftSchema>;
export type DashboardBackupDiskAlert = z.infer<typeof dashboardBackupDiskSchema>;
export type DashboardAlertsResponse = z.infer<typeof dashboardAlertsSchema>;
