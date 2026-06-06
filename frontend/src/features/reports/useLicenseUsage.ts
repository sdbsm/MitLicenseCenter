import { useQuery } from "@tanstack/react-query";
import { api } from "@/lib/api";
import type { LicenseUsageSeriesResponse, ReportsRange } from "./types";

export const reportsQueryKey = ["reports", "license-usage"] as const;

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
export function useLicenseUsage(range: ReportsRange) {
  return useQuery({
    queryKey: [...reportsQueryKey, "summary", range],
    queryFn: () =>
      api<LicenseUsageSeriesResponse>(withQuery("/api/v1/reports/license-usage", range)),
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
        withQuery(`/api/v1/reports/license-usage/${tenantId}`, range)
      ),
    enabled: !!tenantId,
    placeholderData: (prev) => prev,
  });
}
