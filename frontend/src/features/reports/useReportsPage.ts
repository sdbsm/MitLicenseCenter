import { useMemo } from "react";
import { useSearchParams } from "react-router";
import { useAllTenants } from "@/features/tenants/useTenants";
import { buildBackendRange, filtersToUrl, parseFiltersFromUrl } from "./reportsUrlState";
import type { ReportsFilters } from "./types";
import { useLicenseUsage, useLicenseUsageByTenant } from "./useLicenseUsage";

/**
 * Оркестрация страницы «Отчёты»: разбор URL-фильтров (период + выбранный клиент),
 * загрузка сводного ряда и ряда детализации, список клиентов для селектора. Презентация
 * (фильтр, график, сводка, детализация) — в отдельных компонентах (паттерн MLC-032).
 * Период применяется к обеим секциям; tenantId управляет только блоком детализации.
 */
export function useReportsPage() {
  const [searchParams, setSearchParams] = useSearchParams();

  const filters = useMemo(() => parseFiltersFromUrl(searchParams), [searchParams]);
  const range = useMemo(() => buildBackendRange(filters), [filters]);

  const { data: tenantsData } = useAllTenants();
  const tenants = useMemo(() => tenantsData?.items ?? [], [tenantsData]);

  const summary = useLicenseUsage(range);
  const detail = useLicenseUsageByTenant(filters.tenantId, range);

  // Имя выбранного клиента для подписей блока детализации (id может «осиротеть» —
  // тогда имя пустое, ряд приходит пустым 200).
  const selectedTenantName = useMemo(
    () => tenants.find((tenant) => tenant.id === filters.tenantId)?.name ?? null,
    [tenants, filters.tenantId]
  );

  const applyFilters = (next: ReportsFilters) => {
    setSearchParams(filtersToUrl(next), { replace: true });
  };

  const setTenant = (tenantId: string | null) => {
    applyFilters({ ...filters, tenantId });
  };

  return {
    filters,
    tenants,
    selectedTenantName,
    summary,
    detail,
    applyFilters,
    setTenant,
  };
}
