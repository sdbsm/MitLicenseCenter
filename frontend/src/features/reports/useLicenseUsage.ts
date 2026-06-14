import { useQuery } from "@tanstack/react-query";
import { api } from "@/lib/api";
import {
  licenseUsageSeriesResponseSchema,
  type LicenseUsageSeriesResponse,
  type ReportsRange,
} from "./types";
// reportsQueryKey живёт в reportsQueryKeys.ts, чтобы useTenants мог импортировать
// только константу без циклической зависимости через useReportsPage (MLC-122).
import { reportsQueryKey } from "./reportsQueryKeys";
export { reportsQueryKey } from "./reportsQueryKeys";

// Только заданные границы попадают в query — дефолт/кламп диапазона считает сервер.
function buildQuery(range: ReportsRange): string {
  const qs = new URLSearchParams();
  if (range.from) qs.set("from", range.from);
  if (range.to) qs.set("to", range.to);
  return qs.toString();
}

function withQuery(path: string, range: ReportsRange): string {
  const qs = buildQuery(range);
  return qs ? `${path}?${qs}` : path;
}

// Сводка по всем клиентам. Пустой ряд приходит как 200 (buckets:[]) — не ошибка,
// под empty-state «данные накапливаются».
// MLC-132: ответ проходит Zod-валидацию; peakAtUtc опускается бэкендом при null.
export function useLicenseUsage(range: ReportsRange) {
  return useQuery({
    queryKey: [...reportsQueryKey, "summary", range],
    queryFn: () =>
      api<LicenseUsageSeriesResponse>(withQuery("/api/v1/reports/license-usage", range), {
        schema: licenseUsageSeriesResponseSchema,
      }),
    placeholderData: (prev) => prev,
  });
}

// Drill-down одного клиента. Запрос идёт только при выбранном tenantId; неизвестный
// /осиротевший id вернёт пустой ряд 200 (не 404).
export function useLicenseUsageByTenant(tenantId: string | null, range: ReportsRange) {
  return useQuery({
    queryKey: [...reportsQueryKey, "tenant", tenantId, range],
    queryFn: () =>
      api<LicenseUsageSeriesResponse>(
        withQuery(`/api/v1/reports/license-usage/${tenantId}`, range),
        { schema: licenseUsageSeriesResponseSchema }
      ),
    enabled: !!tenantId,
    placeholderData: (prev) => prev,
  });
}
