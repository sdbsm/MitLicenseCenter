import { useMemo } from "react";
import { useSearchParams } from "react-router";
import { useAllTenants } from "@/features/tenants/useTenants";
import {
  buildBackendRange,
  filtersToUrl,
  parseFiltersFromUrl,
  parseReportKind,
} from "./reportsUrlState";
import type { ReportKind, ReportsFilters } from "./types";
import { useDatabaseSize, useDatabaseSizeByTenant } from "./useDatabaseSize";
import { useLicenseUsage, useLicenseUsageByTenant } from "./useLicenseUsage";

/**
 * Оркестрация страницы «Отчёты»: разбор URL (выбранный отчёт + период + клиент),
 * загрузка сводного ряда и ряда детализации для ОБОИХ отчётов (лицензии и размер баз),
 * список клиентов для селектора. Презентация — в отдельных компонентах (паттерн MLC-032).
 * Период/клиент общие для обоих отчётов; report-kind переключает видимую секцию и пишется
 * в URL (`?report=size`), переживая перезагрузку. Лицензии — дефолт (MLC-185f).
 */
export function useReportsPage() {
  const [searchParams, setSearchParams] = useSearchParams();

  const report = useMemo(() => parseReportKind(searchParams), [searchParams]);
  const filters = useMemo(() => parseFiltersFromUrl(searchParams), [searchParams]);
  const range = useMemo(() => buildBackendRange(filters), [filters]);

  const { data: tenantsData } = useAllTenants();
  const tenants = useMemo(() => tenantsData?.items ?? [], [tenantsData]);

  // Лицензии (дефолтный отчёт).
  const summary = useLicenseUsage(range);
  const detail = useLicenseUsageByTenant(filters.tenantId, range);

  // Размер баз (MLC-185f).
  const sizeSummary = useDatabaseSize(range);
  const sizeDetail = useDatabaseSizeByTenant(filters.tenantId, range);

  // Имя выбранного клиента для подписей блока детализации (id может «осиротеть» —
  // тогда имя пустое, ряд приходит пустым 200).
  const selectedTenantName = useMemo(
    () => tenants.find((tenant) => tenant.id === filters.tenantId)?.name ?? null,
    [tenants, filters.tenantId]
  );

  const applyFilters = (next: ReportsFilters) => {
    setSearchParams(filtersToUrl(next, report), { replace: true });
  };

  const setTenant = (tenantId: string | null) => {
    applyFilters({ ...filters, tenantId });
  };

  const setReport = (next: ReportKind) => {
    setSearchParams(filtersToUrl(filters, next), { replace: true });
  };

  return {
    report,
    setReport,
    filters,
    tenants,
    selectedTenantName,
    summary,
    detail,
    sizeSummary,
    sizeDetail,
    applyFilters,
    setTenant,
  };
}
