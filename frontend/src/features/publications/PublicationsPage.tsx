import { useTranslation } from "react-i18next";
import { Button } from "@/components/ui/button";
import { TooltipProvider } from "@/components/ui/tooltip";
import { ChangePlatformDialog } from "./ChangePlatformDialog";
import { PublicationsFiltersBar } from "./PublicationsFiltersBar";
import { PublicationsTable } from "./PublicationsTable";
import { PublishPublicationDialog } from "./PublishPublicationDialog";
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
    status,
    setFilter,
    filtered,
    hasAnyPublications,
    checkingId,
    handleCheck,
    publishTarget,
    openPublish,
    platformTarget,
    openPlatform,
  } = usePublicationsPage();

  return (
    <TooltipProvider delayDuration={150}>
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
          status={status}
          onChange={setFilter}
        />

        <PublicationsTable
          rows={filtered}
          isLoading={isLoading}
          isError={isError}
          isAdmin={isAdmin}
          hasAnyPublications={hasAnyPublications}
          checkingId={checkingId}
          onCheck={handleCheck}
          onPublish={openPublish}
          onChangePlatform={openPlatform}
        />

        <PublishPublicationDialog
          key={`publish-${publishTarget?.id ?? "new"}`}
          open={publishTarget !== null}
          onOpenChange={(open) => {
            if (!open) openPublish(null);
          }}
          publication={publishTarget}
        />

        <ChangePlatformDialog
          key={`platform-${platformTarget?.id ?? "new"}`}
          open={platformTarget !== null}
          onOpenChange={(open) => {
            if (!open) openPlatform(null);
          }}
          publication={platformTarget}
        />
      </div>
    </TooltipProvider>
  );
}
