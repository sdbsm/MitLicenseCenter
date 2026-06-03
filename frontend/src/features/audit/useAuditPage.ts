import { useMemo } from "react";
import { useSearchParams } from "react-router";
import { useAllTenants } from "@/features/tenants/useTenants";
import { buildBackendFilters, filtersToUrl, parseFiltersFromUrl } from "./auditUrlState";
import { isFilterBeyondRetention, retentionCutoffDate } from "./retention";
import type { AuditFilters } from "./types";
import { useAuditLog } from "./useAuditLog";
import { useAuditRetention } from "./useAuditRetention";

/**
 * Оркестрация страницы аудита: разбор URL-фильтров, загрузка журнала/клиентов/настройки
 * хранения, пагинация и логика баннера ретенции. Презентация (фильтры, таблица, пагинация)
 * вынесена в отдельные компоненты — поведение 1:1 с прежней монолитной страницей (MLC-032).
 */
export function useAuditPage() {
  const [searchParams, setSearchParams] = useSearchParams();

  const filters = useMemo(() => parseFiltersFromUrl(searchParams), [searchParams]);
  const backendFilters = useMemo(() => buildBackendFilters(filters), [filters]);

  const { data: tenantsData } = useAllTenants();
  const tenantNameById = useMemo(() => {
    const map = new Map<string, string>();
    for (const tenant of tenantsData?.items ?? []) {
      map.set(tenant.id, tenant.name);
    }
    return map;
  }, [tenantsData]);

  const { data, isLoading, isError, refetch, isFetching } = useAuditLog(backendFilters);
  const items = data?.items ?? [];
  const total = data?.total ?? 0;
  const totalPages = Math.max(1, Math.ceil(total / filters.pageSize));
  const currentPage = Math.min(filters.page, totalPages);

  // PR 4.3: banner показывается если `from` фильтр глубже окна хранения. `nowUtc`
  // фиксируем на mount через useMemo — иначе banner-threshold будет дёргаться на
  // каждом render'е filter-state и при долгом сидении страницы. Operator reload'нёт
  // при необходимости — acceptable trade-off vs. setInterval-обновление.
  const { data: retention } = useAuditRetention();
  const nowUtc = useMemo(() => new Date(), []);
  const showRetentionBanner = isFilterBeyondRetention(
    filters.from,
    retention?.retentionDays,
    nowUtc
  );
  const retentionCutoff = retentionCutoffDate(retention?.retentionDays, nowUtc);

  const applyFilters = (next: AuditFilters) => {
    setSearchParams(filtersToUrl(next), { replace: true });
  };

  const goToPage = (page: number) => {
    if (page < 1 || page > totalPages || page === currentPage) return;
    applyFilters({ ...filters, page });
  };

  return {
    filters,
    items,
    total,
    totalPages,
    currentPage,
    tenantNameById,
    isLoading,
    isError,
    isFetching,
    refetch,
    showRetentionBanner,
    retentionCutoff,
    applyFilters,
    goToPage,
  };
}
