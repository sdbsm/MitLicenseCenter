import { useMemo } from "react";
import { useTranslation } from "react-i18next";
import { toast } from "sonner";
import { Button } from "@/components/ui/button";
import { LicenseUsageSummary } from "@/features/reports/LicenseUsageSummary";
import { ReportsDetail } from "@/features/reports/ReportsDetail";
import { ReportsFiltersBar } from "@/features/reports/ReportsFiltersBar";
import { useReportFilters } from "@/features/reports/useReportFilters";
import { useLicenseUsage, useLicenseUsageByTenant } from "@/features/reports/useLicenseUsage";
import { useAllTenants } from "@/features/tenants/useTenants";

/**
 * MLC-196b: вид «Использование лицензий» (за период) внутри страницы «Сеансы» —
 * license-часть растворённых «Отчётов». Самодостаточный компонент: рендерится ВНУТРИ
 * `TabsContent value="usage"`, поэтому Radix монтирует его только когда вид активен →
 * отчётные хуки (`useLicenseUsage`/`useLicenseUsageByTenant`) не стреляют на «По
 * клиентам»/«Живые сеансы». Период/клиент держатся в URL (`from`/`to`/`tenant`); запись —
 * слиянием (`useReportFilters`), хост-ключ `view=usage` сохраняется. Эндпоинты те же
 * (`/reports/license-usage[/:tenantId]`). Образец вёрстки — вкладка license `ReportsPage`.
 */
export function SessionsUsageView() {
  const { t } = useTranslation();
  const { filters, range, applyFilters, setTenant } = useReportFilters();

  const { data: tenantsData } = useAllTenants();
  const tenants = useMemo(() => tenantsData?.items ?? [], [tenantsData]);

  const summary = useLicenseUsage(range);
  const detail = useLicenseUsageByTenant(filters.tenantId, range);

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
          <p className="font-medium">{t("reports.errors.loadFailed")}</p>
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

      <LicenseUsageSummary data={summary.data} isLoading={summary.isLoading} />
      <ReportsDetail
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
