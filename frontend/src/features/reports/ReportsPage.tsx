import { useTranslation } from "react-i18next";
import { toast } from "sonner";
import { Button } from "@/components/ui/button";
import { LicenseUsageSummary } from "./LicenseUsageSummary";
import { ReportsDetail } from "./ReportsDetail";
import { ReportsFiltersBar } from "./ReportsFiltersBar";
import { useReportsPage } from "./useReportsPage";

/** Раздел «Отчёты» — использование лицензий (MLC-050). Контейнер: заголовок, фильтр
 *  периода, секция сводки и блок детализации по клиенту (паттерн MLC-032). */
export function ReportsPage() {
  const { t } = useTranslation();
  const { filters, tenants, selectedTenantName, summary, detail, applyFilters, setTenant } =
    useReportsPage();

  return (
    <div className="space-y-6">
      <div className="space-y-1">
        <h2 className="text-2xl font-semibold tracking-tight">{t("reports.title")}</h2>
        <p className="text-muted-foreground text-sm">{t("reports.subtitle")}</p>
      </div>

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
