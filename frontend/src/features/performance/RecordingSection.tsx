import { CircleIcon, CircleStopIcon, FileVideoIcon, PlayIcon } from "lucide-react";
import { useMemo, useState } from "react";
import { useTranslation } from "react-i18next";
import { toast } from "sonner";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Skeleton } from "@/components/ui/skeleton";
import { useMe } from "@/features/auth/useAuth";
import { matchConflictCode } from "@/lib/apiErrors";
import { DeleteRecordingDialog } from "./DeleteRecordingDialog";
import { RecordingDetailDialog } from "./RecordingDetailDialog";
import { RecordingsTable } from "./RecordingsTable";
import { StopRecordingDialog } from "./StopRecordingDialog";
import type { RecordingSummary } from "./types";
import { useRecordings, useStartRecording } from "./useRecordings";

/**
 * Запись по требованию (MLC-070/071, ADR-26, Фаза 4) — секция ниже трёх live-источников раздела
 * «Быстродействие». В отличие от них запись персистится: оператор включает её для расследования,
 * бэкенд пишет сэмплы по таймеру до ручного стопа или авто-стопа. Управление (старт/стоп/удаление)
 * = Admin (гейт по `useMe`); список/просмотр = Viewer. Индикатор «идёт запись» виден всем.
 */
export function RecordingSection() {
  const { t } = useTranslation();
  const { data: me } = useMe();
  const isAdmin = me?.roles?.includes("Admin") ?? false;

  const { data, isLoading, isError } = useRecordings();
  const start = useStartRecording();

  const recordings = useMemo<RecordingSummary[]>(() => data ?? [], [data]);
  const active = recordings.find((r) => r.status === "Active") ?? null;

  const [viewing, setViewing] = useState<string | null>(null);
  const [stopping, setStopping] = useState<string | null>(null);
  const [deleting, setDeleting] = useState<string | null>(null);

  const handleStart = async () => {
    try {
      await start.mutateAsync();
      toast.success(t("performance.recording.toasts.started"));
    } catch (error) {
      const messageKey = matchConflictCode(error, {
        RECORDING_ACTIVE: "performance.recording.errors.active",
      });
      toast.error(messageKey ? t(messageKey) : t("errors.generic"));
    }
  };

  return (
    <Card>
      <CardHeader>
        <div className="flex flex-wrap items-start justify-between gap-3">
          <div className="space-y-1">
            <CardTitle>{t("performance.recording.title")}</CardTitle>
            <p className="text-muted-foreground text-sm">{t("performance.recording.subtitle")}</p>
          </div>
          <div className="flex items-center gap-3">
            {active && (
              <span className="flex items-center gap-1.5 text-sm font-medium text-rose-600 dark:text-rose-400">
                <CircleIcon className="size-3 animate-pulse fill-current" />
                {t("performance.recording.recordingNow")}
              </span>
            )}
            {isAdmin &&
              (active ? (
                <Button variant="outline" size="sm" onClick={() => setStopping(active.id)}>
                  <CircleStopIcon className="size-4" />
                  {t("performance.recording.actions.stop")}
                </Button>
              ) : (
                <Button
                  size="sm"
                  disabled={start.isPending}
                  onClick={() => {
                    void handleStart();
                  }}
                >
                  <PlayIcon className="size-4" />
                  {t("performance.recording.actions.start")}
                </Button>
              ))}
          </div>
        </div>
      </CardHeader>
      <CardContent>
        {isError && !data && (
          <p className="text-muted-foreground text-sm">
            {t("performance.recording.errors.loadFailed")}
          </p>
        )}

        {isLoading && !data ? (
          <Skeleton className="h-40 w-full" />
        ) : recordings.length === 0 ? (
          <div className="flex flex-col items-center justify-center gap-3 py-10 text-center">
            <FileVideoIcon className="text-muted-foreground size-8" />
            <div className="space-y-1">
              <p className="font-medium">{t("performance.recording.empty.title")}</p>
              <p className="text-muted-foreground text-sm">
                {t("performance.recording.empty.hint")}
              </p>
            </div>
          </div>
        ) : (
          <RecordingsTable
            recordings={recordings}
            isAdmin={isAdmin}
            onView={(r) => setViewing(r.id)}
            onDelete={(r) => setDeleting(r.id)}
          />
        )}
      </CardContent>

      <RecordingDetailDialog
        recordingId={viewing}
        open={viewing !== null}
        onOpenChange={(open) => {
          if (!open) setViewing(null);
        }}
      />
      <StopRecordingDialog
        recordingId={stopping}
        open={stopping !== null}
        onOpenChange={(open) => {
          if (!open) setStopping(null);
        }}
      />
      <DeleteRecordingDialog
        recordingId={deleting}
        open={deleting !== null}
        onOpenChange={(open) => {
          if (!open) setDeleting(null);
        }}
      />
    </Card>
  );
}
