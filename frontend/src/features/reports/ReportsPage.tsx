import { useTranslation } from "react-i18next";
import { toast } from "sonner";
import { Button } from "@/components/ui/button";
import { Tabs, TabsContent, TabsList, TabsTrigger } from "@/components/ui/tabs";
import { DatabaseSizeDetail } from "./DatabaseSizeDetail";
import { DatabaseSizeSummary } from "./DatabaseSizeSummary";
import { LicenseUsageSummary } from "./LicenseUsageSummary";
import { ReportsDetail } from "./ReportsDetail";
import { ReportsFiltersBar } from "./ReportsFiltersBar";
import type { ReportKind } from "./types";
import { useReportsPage } from "./useReportsPage";

/** Раздел «Отчёты»: переключатель отчётов (использование лицензий ↔ размер баз, MLC-185f),
 *  общий фильтр периода, секция сводки и блок детализации по клиенту (паттерн MLC-032).
 *  Выбранный отчёт хранится в URL (`?report=size`); лицензии — дефолт. */
export function ReportsPage() {
  const { t } = useTranslation();
  const {
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
  } = useReportsPage();

  // Плашка клампа/ошибки относится к активному отчёту.
  const active = report === "size" ? sizeSummary : summary;

  return (
    <div className="space-y-6">
      <div className="space-y-1">
        <h2 className="text-2xl font-semibold tracking-tight">{t("reports.title")}</h2>
        <p className="text-muted-foreground text-sm">{t("reports.subtitle")}</p>
      </div>

      <Tabs value={report} onValueChange={(value) => setReport(value as ReportKind)}>
        <TabsList>
          <TabsTrigger value="license">{t("reports.tabs.license")}</TabsTrigger>
          <TabsTrigger value="size">{t("reports.tabs.size")}</TabsTrigger>
        </TabsList>

        <ReportsFiltersBar filters={filters} onChange={applyFilters} />

        {active.data?.clamped && (
          <div className="bg-muted/30 text-muted-foreground rounded-md border p-3 text-sm">
            {t("reports.filters.clampNotice", { days: active.data.maxSpanDays })}
          </div>
        )}

        {active.isError && (
          <div className="border-destructive/40 bg-destructive/5 rounded-md border p-4 text-sm">
            <p className="font-medium">
              {report === "size"
                ? t("reports.size.errors.loadFailed")
                : t("reports.errors.loadFailed")}
            </p>
            <Button
              variant="link"
              className="px-0"
              onClick={() => {
                void active.refetch().then((r) => {
                  if (r.isSuccess) toast.success(t("common.refresh"));
                });
              }}
            >
              {t("common.refresh")}
            </Button>
          </div>
        )}

        <TabsContent value="license" className="space-y-6">
          <LicenseUsageSummary data={summary.data} isLoading={summary.isLoading} />
          <ReportsDetail
            tenants={tenants}
            selectedTenantId={filters.tenantId}
            selectedTenantName={selectedTenantName}
            data={detail.data}
            isLoading={detail.isLoading}
            onSelectTenant={setTenant}
          />
        </TabsContent>

        <TabsContent value="size" className="space-y-6">
          <DatabaseSizeSummary data={sizeSummary.data} isLoading={sizeSummary.isLoading} />
          <DatabaseSizeDetail
            tenants={tenants}
            selectedTenantId={filters.tenantId}
            selectedTenantName={selectedTenantName}
            data={sizeDetail.data}
            isLoading={sizeDetail.isLoading}
            onSelectTenant={setTenant}
          />
        </TabsContent>
      </Tabs>
    </div>
  );
}
