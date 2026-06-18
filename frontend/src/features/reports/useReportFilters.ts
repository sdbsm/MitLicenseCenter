import { useMemo } from "react";
import { useSearchParams } from "react-router";
import {
  buildBackendRange,
  mergeReportFiltersIntoParams,
  parseFiltersFromUrl,
} from "./reportsUrlState";
import type { ReportsFilters, ReportsRange } from "./types";

/**
 * MLC-196b: общий хук фильтров встроенного отчёта (период + клиент) для домов «Сеансы»
 * и «Базы», куда растворены «Отчёты». Читает `from`/`to`/`tenant` из URL и строит
 * backend-range, а запись делает СЛИЯНИЕМ в существующие параметры
 * (`mergeReportFiltersIntoParams`) — хост-ключ страницы (`view`/`tab`) и прочее
 * сохраняются, вид/вкладка не сбрасываются. Это и есть ключевое отличие от старого
 * `useReportsPage`, который делал полный replace URL (для отдельной страницы `/reports`
 * это было безопасно — там хост-ключей нет).
 */
export interface UseReportFiltersResult {
  filters: ReportsFilters;
  range: ReportsRange;
  applyFilters: (next: ReportsFilters) => void;
  setTenant: (tenantId: string | null) => void;
}

export function useReportFilters(): UseReportFiltersResult {
  const [searchParams, setSearchParams] = useSearchParams();

  const filters = useMemo(() => parseFiltersFromUrl(searchParams), [searchParams]);
  const range = useMemo(() => buildBackendRange(filters), [filters]);

  const applyFilters = (next: ReportsFilters) => {
    setSearchParams((prev) => mergeReportFiltersIntoParams(prev, next), { replace: true });
  };

  const setTenant = (tenantId: string | null) => {
    applyFilters({ ...filters, tenantId });
  };

  return { filters, range, applyFilters, setTenant };
}
