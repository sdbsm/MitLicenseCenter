import { useQuery } from "@tanstack/react-query";
import { api } from "@/lib/api";
import { databaseSizeQueryKey } from "./reportsQueryKeys";
import {
  databaseSizeSeriesResponseSchema,
  databaseSizeTenantSeriesResponseSchema,
  type DatabaseSizeSeriesResponse,
  type DatabaseSizeTenantSeriesResponse,
  type ReportsRange,
} from "./types";

// Только заданные границы попадают в query — дефолт/кламп диапазона считает сервер
// (та же конвенция, что у лицензий).
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

// Сводка размера баз по хосту: ряд итога во времени + разбивка по клиентам на последний
// снимок периода. Пустой период приходит как 200 (points:[], tenants:[]) — не ошибка,
// под empty-state. Ответ проходит Zod; tenantId/tenantName опускаются для «без клиента».
export function useDatabaseSize(range: ReportsRange) {
  return useQuery({
    queryKey: [...databaseSizeQueryKey, "summary", range],
    queryFn: () =>
      api<DatabaseSizeSeriesResponse>(withQuery("/api/v1/reports/database-size", range), {
        schema: databaseSizeSeriesResponseSchema,
      }),
    placeholderData: (prev) => prev,
  });
}

// Drill-down одного клиента: ряд клиента во времени + строки его баз. Запрос идёт только
// при выбранном tenantId; неизвестный/осиротевший id вернёт пустой ряд 200 (не 404).
export function useDatabaseSizeByTenant(tenantId: string | null, range: ReportsRange) {
  return useQuery({
    queryKey: [...databaseSizeQueryKey, "tenant", tenantId, range],
    queryFn: () =>
      api<DatabaseSizeTenantSeriesResponse>(
        withQuery(`/api/v1/reports/database-size/${tenantId}`, range),
        { schema: databaseSizeTenantSeriesResponseSchema }
      ),
    enabled: !!tenantId,
    placeholderData: (prev) => prev,
  });
}
