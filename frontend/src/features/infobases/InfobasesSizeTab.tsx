import { useMemo } from "react";
import { useTranslation } from "react-i18next";
import { toast } from "sonner";
import { Button } from "@/components/ui/button";
import { DatabaseSizeDetail } from "@/features/reports/DatabaseSizeDetail";
import { DatabaseSizeSummary } from "@/features/reports/DatabaseSizeSummary";
import { ReportsFiltersBar } from "@/features/reports/ReportsFiltersBar";
import { useReportFilters } from "@/features/reports/useReportFilters";
import { useDatabaseSize, useDatabaseSizeByTenant } from "@/features/reports/useDatabaseSize";
import { useAllTenants } from "@/features/tenants/useTenants";

/**
 * MLC-196b: вкладка «Размер баз» (за период) внутри страницы «Базы» — size-часть
 * растворённых «Отчётов». Самодостаточный компонент: рендерится ВНУТРИ
 * `TabsContent value="size"`, поэтому Radix монтирует его только когда вкладка активна →
 * отчётные хуки (`useDatabaseSize`/`useDatabaseSizeByTenant`) не стреляют на «Базы»/«IIS».
 * Период/клиент держатся в URL (`from`/`to`/`tenant`); запись — слиянием
 * (`useReportFilters`), хост-ключ `tab=size` сохраняется. Отчётный `?tenant=` ≠ басовый
 * `?tenantId=` (разные ключи). Эндпоинты те же (`/reports/database-size[/:tenantId]`).
 * Образец вёрстки — прежняя вкладка size страницы `/reports` (удалена в MLC-196c).
 */
export function InfobasesSizeTab() {
  const { t } = useTranslation();
  const { filters, range, applyFilters, setTenant } = useReportFilters();

  const { data: tenantsData } = useAllTenants();
  const tenants = useMemo(() => tenantsData?.items ?? [], [tenantsData]);

  const summary = useDatabaseSize(range);
  const detail = useDatabaseSizeByTenant(filters.tenantId, range);

  const selectedTenantName = useMemo(
    () => tenants.find((tenant) => tenant.id === filters.tenantId)?.name ?? null,
    [tenants, filters.tenantId]
  );

  return (
    <div className="space-y-6">
      <ReportsFiltersBar filters={filters} onChange={applyFilters} />

      {summary.data?.clamped && (
        <div className="bg-muted/30 text-muted-foreground rounded-md border p-3 text-sm">
          {t("reports.filters.clampNotice", { days: summary.data.maxSpanDays })}
        </div>
      )}

      {summary.isError && (
        <div className="border-destructive/40 bg-destructive/5 rounded-md border p-4 text-sm">
          <p className="font-medium">{t("reports.size.errors.loadFailed")}</p>
          <Button
            variant="link"
            className="px-0"
            onClick={() => {
              void summary.refetch().then((r) => {
                if (r.isSuccess) toast.success(t("common.refresh"));
              });
            }}
          >
            {t("common.refresh")}
          </Button>
        </div>
      )}

      <DatabaseSizeSummary data={summary.data} isLoading={summary.isLoading} />
      <DatabaseSizeDetail
        tenants={tenants}
        selectedTenantId={filters.tenantId}
        selectedTenantName={selectedTenantName}
        data={detail.data}
        isLoading={detail.isLoading}
        onSelectTenant={setTenant}
      />
    </div>
  );
}
