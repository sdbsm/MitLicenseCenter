import { useTranslation } from "react-i18next";
import { toast } from "sonner";
import { RelativeTime } from "@/components/ui/RelativeTime";
import { Button } from "@/components/ui/button";
import { TooltipProvider } from "@/components/ui/tooltip";
import { KillSessionDialog } from "./KillSessionDialog";
import { SessionsFiltersBar } from "./SessionsFiltersBar";
import { SessionsTable } from "./SessionsTable";
import { useSessionsPage } from "./useSessionsPage";

export function SessionsPage() {
  const { t } = useTranslation();
  const {
    snapshot,
    isLoading,
    isError,
    refetch,
    failureCount,
    isAdmin,
    infobases,
    q,
    infobaseId,
    filtered,
    setFilter,
    selectedSession,
    killOpen,
    handleKillClick,
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

        {/* Filter bar */}
        <SessionsFiltersBar
          q={q}
          infobaseId={infobaseId}
          infobases={infobases}
          onChange={setFilter}
        />

        {/* Table */}
        <SessionsTable
          rows={filtered}
          isLoading={isLoading}
          isError={isError}
          isAdmin={isAdmin}
          onKill={handleKillClick}
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
