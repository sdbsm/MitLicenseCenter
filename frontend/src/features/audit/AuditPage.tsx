import { format } from "date-fns";
import { ru } from "date-fns/locale";
import { useTranslation } from "react-i18next";
import { toast } from "sonner";
import { Button } from "@/components/ui/button";
import { TooltipProvider } from "@/components/ui/tooltip";
import { Alert, AlertDescription, AlertTitle } from "@/components/ui/alert";
import { AuditFiltersBar } from "./AuditFiltersBar";
import { AuditPagination } from "./AuditPagination";
import { AuditTable } from "./AuditTable";
import { useAuditPage } from "./useAuditPage";

export function AuditPage() {
  const { t } = useTranslation();
  const {
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
  } = useAuditPage();

  return (
    <TooltipProvider delayDuration={150}>
      <div className="space-y-6">
        <div className="flex items-start justify-between gap-4">
          <div className="space-y-1">
            <h2 className="text-2xl font-semibold tracking-tight">{t("audit.title")}</h2>
            <p className="text-muted-foreground text-sm">{t("audit.subtitle")}</p>
          </div>
        </div>

        {showRetentionBanner && retentionCutoff && (
          <Alert>
            <AlertTitle>{t("audit.retention.bannerTitle")}</AlertTitle>
            <AlertDescription>
              {t("audit.retention.bannerText", {
                date: format(retentionCutoff, "dd.MM.yyyy", { locale: ru }),
              })}
            </AlertDescription>
          </Alert>
        )}

        <AuditFiltersBar filters={filters} onChange={applyFilters} />

        {isError && (
          <div className="border-destructive/40 bg-destructive/5 rounded-md border p-4 text-sm">
            <p className="font-medium">{t("audit.errors.loadFailed")}</p>
            <Button
              variant="link"
              className="px-0"
              onClick={() => {
                void refetch().then((r) => {
                  if (r.isSuccess) toast.success(t("common.refresh"));
                });
              }}
            >
              {t("common.refresh")}
            </Button>
          </div>
        )}

        <AuditTable
          items={items}
          isLoading={isLoading}
          isError={isError}
          tenantNameById={tenantNameById}
        />

        {total > filters.pageSize && (
          <AuditPagination
            currentPage={currentPage}
            totalPages={totalPages}
            pageSize={filters.pageSize}
            total={total}
            onPageChange={goToPage}
          />
        )}

        {isFetching && !isLoading && (
          <p className="text-muted-foreground text-xs">{t("audit.pagination.refreshing")}</p>
        )}
      </div>
    </TooltipProvider>
  );
}
