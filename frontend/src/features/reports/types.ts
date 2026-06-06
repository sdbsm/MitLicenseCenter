// Зеркало контракта MLC-049 `LicenseUsageSeriesResponse` (camelCase JSON,
// JsonStringEnum/camelCase — дефолт ASP.NET). Оба эндпоинта (сводка и drill-down)
// возвращают ОДНУ форму, поэтому график рисуется одним компонентом.

export interface LicenseUsageBucketPoint {
  bucketStartUtc: string;
  consumedAvg: number;
  consumedMax: number;
  limit: number;
}

export interface LicenseUsageSeriesResponse {
  buckets: LicenseUsageBucketPoint[];
  // Эффективный диапазон ПОСЛЕ дефолта/клампа на сервере (7 дней / 31 день) —
  // показываем его, а не запрошенный (см. ReportsEndpoints.ResolveRange).
  fromUtc: string;
  toUtc: string;
  peakConsumed: number;
  peakLimit: number;
  peakAtUtc: string | null;
  averageConsumed: number;
}

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
