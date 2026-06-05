import { useTranslation } from "react-i18next";
import { Button } from "@/components/ui/button";
import { TooltipProvider } from "@/components/ui/tooltip";
import { BulkChangePlatformDialog } from "./BulkChangePlatformDialog";
import { BulkPublishDialog } from "./BulkPublishDialog";
import { ChangePlatformDialog } from "./ChangePlatformDialog";
import { IisManagementCard } from "./iis/IisManagementCard";
import { PublicationsBulkBar } from "./PublicationsBulkBar";
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
    selectedIds,
    toggleSelect,
    toggleAll,
    clearSelection,
    deselectSucceeded,
    selectedPublications,
    bulkPublishOpen,
    setBulkPublishOpen,
    bulkPlatformOpen,
    setBulkPlatformOpen,
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

        <IisManagementCard isAdmin={isAdmin} />

        <PublicationsFiltersBar
          tenants={tenants}
          tenantId={tenantId}
          status={status}
          onChange={setFilter}
        />

        {isAdmin && selectedIds.size > 0 && (
          <PublicationsBulkBar
            count={selectedIds.size}
            onPublish={() => setBulkPublishOpen(true)}
            onChangePlatform={() => setBulkPlatformOpen(true)}
            onClear={clearSelection}
          />
        )}

        <PublicationsTable
          rows={filtered}
          isLoading={isLoading}
          isError={isError}
          isAdmin={isAdmin}
          hasAnyPublications={hasAnyPublications}
          checkingId={checkingId}
          selectedIds={selectedIds}
          onToggleSelect={toggleSelect}
          onToggleAll={toggleAll}
          onCheck={handleCheck}
          onPublish={openPublish}
          onChangePlatform={openPlatform}
        />

        {isAdmin && (
          <>
            <BulkPublishDialog
              open={bulkPublishOpen}
              onOpenChange={setBulkPublishOpen}
              publications={selectedPublications}
              onRunComplete={deselectSucceeded}
            />
            <BulkChangePlatformDialog
              open={bulkPlatformOpen}
              onOpenChange={setBulkPlatformOpen}
              publications={selectedPublications}
              onRunComplete={deselectSucceeded}
            />
          </>
        )}

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
