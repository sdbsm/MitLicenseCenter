import { useTranslation } from "react-i18next";
import { Button } from "@/components/ui/button";
import { PublicationsFiltersBar } from "./PublicationsFiltersBar";
import { PublicationsTable } from "./PublicationsTable";
import { ReconcilePublicationDialog } from "./ReconcilePublicationDialog";
import { usePublicationsPage } from "./usePublicationsPage";

export function PublicationsPage() {
  const { t } = useTranslation();
  const {
    isLoading,
    isError,
    refetch,
    isAdmin,
    tenants,
    tenantId,
    driftStatus,
    setFilter,
    filtered,
    hasAnyPublications,
    pollingId,
    handleCheckDrift,
    selectedPublication,
    reconcileOpen,
    handleReconcileClick,
    handleReconcileOpenChange,
  } = usePublicationsPage();

  return (
    <div className="space-y-6">
      <div className="flex items-start justify-between gap-4">
        <div className="space-y-1">
          <h2 className="text-2xl font-semibold tracking-tight">{t("publications.title")}</h2>
        </div>
      </div>

      {isError && (
        <div className="border-destructive/40 bg-destructive/5 rounded-md border p-4 text-sm">
          <p className="font-medium">{t("errors.generic")}</p>
          <Button
            variant="link"
            className="px-0"
            onClick={() => {
              void refetch();
            }}
          >
            {t("common.refresh")}
          </Button>
        </div>
      )}

      <PublicationsFiltersBar
        tenants={tenants}
        tenantId={tenantId}
        driftStatus={driftStatus}
        onChange={setFilter}
      />

      <PublicationsTable
        rows={filtered}
        isLoading={isLoading}
        isError={isError}
        isAdmin={isAdmin}
        hasAnyPublications={hasAnyPublications}
        pollingId={pollingId}
        onCheckDrift={handleCheckDrift}
        onReconcile={handleReconcileClick}
      />

      <ReconcilePublicationDialog
        key={selectedPublication?.id ?? "new"}
        open={reconcileOpen}
        onOpenChange={handleReconcileOpenChange}
        publication={selectedPublication}
      />
    </div>
  );
}
