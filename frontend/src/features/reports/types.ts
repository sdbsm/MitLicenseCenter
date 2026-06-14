import { z } from "zod";
import { omittable } from "@/lib/apiSchema";

/**
 * Zod-схемы ответов отчётов (MLC-132, FE-09).
 * Зеркало LicenseUsageSeriesResponse / LicenseUsageBucketPoint (ReportsContracts.cs).
 * Backend опускает null-поля (JsonIgnoreCondition.WhenWritingNull, ADR-32):
 * peakAtUtc=null (нет данных) → ключ отсутствует → omittable().
 * Оба эндпоинта (/reports/license-usage и /reports/license-usage/:tenantId) возвращают
 * одну форму — одна схема покрывает оба.
 */

export const licenseUsageBucketPointSchema = z.object({
  bucketStartUtc: z.string(),
  consumedAvg: z.number(),
  consumedMax: z.number(),
  limit: z.number(),
});

export const licenseUsageSeriesResponseSchema = z.object({
  buckets: z.array(licenseUsageBucketPointSchema),
  // Эффективный диапазон ПОСЛЕ дефолта/клампа на сервере (7 дней / 31 день).
  fromUtc: z.string(),
  toUtc: z.string(),
  peakConsumed: z.number(),
  peakLimit: z.number(),
  // peakAtUtc=null → бэкенд опускает ключ (нет данных в периоде).
  peakAtUtc: omittable(z.string()),
  averageConsumed: z.number(),
  // MLC-054: clamped=true → сервер обрезал запрошенную ширину до maxSpanDays.
  clamped: z.boolean(),
  maxSpanDays: z.number(),
});

export type LicenseUsageBucketPoint = z.infer<typeof licenseUsageBucketPointSchema>;
export type LicenseUsageSeriesResponse = z.infer<typeof licenseUsageSeriesResponseSchema>;

// UI-состояние страницы: общий период (date-only из `<input type="date">`) +
// выбранный для детализации клиент. Период применяется к обеим секциям, tenantId —
// только к блоку детализации (сводка видна всегда).
export interface ReportsFilters {
  from: string | null;
  to: string | null;
  tenantId: string | null;
}

// Диапазон, уходящий в query backend (ISO с временем). Пустые границы не ставятся —
// дефолт/кламп считает сервер.
export interface ReportsRange {
  from: string | null;
  to: string | null;
}
