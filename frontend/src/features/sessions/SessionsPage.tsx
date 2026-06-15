import { useTranslation } from "react-i18next";
import { toast } from "sonner";
import { LiveControls } from "@/components/LiveControls";
import { PaginationBar } from "@/components/PaginationBar";
import { RelativeTime } from "@/components/ui/RelativeTime";
import { Button } from "@/components/ui/button";
import { TooltipProvider } from "@/components/ui/tooltip";
import { KillSessionDialog } from "./KillSessionDialog";
import { SessionsFiltersBar } from "./SessionsFiltersBar";
import { SessionsTable } from "./SessionsTable";
import { SESSIONS_PAGE_SIZE, useSessionsPage } from "./useSessionsPage";

export function SessionsPage() {
  const { t } = useTranslation();
  const {
    snapshot,
    isLoading,
    isError,
    refetch,
    failureCount,
    isPaused,
    togglePause,
    refreshNow,
    isRefreshing,
    isAdmin,
    infobases,
    q,
    infobaseId,
    table,
    density,
    toggleDensity,
    page,
    totalFiltered,
    setPage,
    setFilter,
    selectedSession,
    killOpen,
    handleKillOpenChange,
  } = useSessionsPage();

  return (
    <TooltipProvider delayDuration={150}>
      <div className="space-y-6">
        {/* Header */}
        <div className="flex items-start justify-between gap-4">
          <div className="space-y-1">
            <h2 className="text-2xl font-semibold tracking-tight">{t("sessions.title")}</h2>
            <p className="text-muted-foreground text-sm">{t("sessions.subtitle")}</p>
          </div>
          <div className="flex items-center gap-3">
            {snapshot && (
              <span className="text-muted-foreground flex items-center gap-1 text-sm">
                {t("sessions.freshness.label")}{" "}
                <RelativeTime
                  value={snapshot.capturedAt}
                  thresholdAmberSec={60}
                  isError={failureCount >= 2}
                />
              </span>
            )}
            <LiveControls
              isPaused={isPaused}
              onTogglePause={togglePause}
              onRefreshNow={() => void refreshNow()}
              isRefreshing={isRefreshing}
            />
          </div>
        </div>

        {/* Error banner */}
        {isError && (
          <div className="border-destructive/40 bg-destructive/5 rounded-md border p-4 text-sm">
            <p className="font-medium">{t("sessions.errors.loadFailed")}</p>
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

        {/* License-fact unavailable banner (ADR-48, MLC-166): факт rac --licenses не
            получен → потребление может быть неактуальным, авто-завершение на паузе. */}
        {snapshot && !snapshot.licenseFactAvailable && (
          <div
            role="status"
            className="rounded-md border border-amber-500/40 bg-amber-500/5 p-4 text-sm text-amber-800 dark:text-amber-300"
          >
            <p className="font-medium">{t("sessions.licenseFactUnavailable")}</p>
          </div>
        )}

        {/* Table — фильтры в тулбаре DataTable, рядом с видимостью колонок и density */}
        <SessionsTable
          table={table}
          isLoading={isLoading}
          isError={isError}
          isAdmin={isAdmin}
          density={density}
          onToggleDensity={toggleDensity}
          filters={
            <SessionsFiltersBar
              q={q}
              infobaseId={infobaseId}
              infobases={infobases}
              onChange={setFilter}
            />
          }
        />

        {/* Pagination */}
        <PaginationBar
          page={page}
          pageSize={SESSIONS_PAGE_SIZE}
          total={totalFiltered}
          onPageChange={setPage}
          isFetching={false}
        />

        <KillSessionDialog
          key={selectedSession?.sessionId ?? "new"}
          open={killOpen}
          onOpenChange={handleKillOpenChange}
          session={selectedSession}
        />
      </div>
    </TooltipProvider>
  );
}
