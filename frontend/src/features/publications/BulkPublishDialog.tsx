import { useQueryClient } from "@tanstack/react-query";
import { useCallback, useMemo } from "react";
import { useTranslation } from "react-i18next";
import { Button } from "@/components/ui/button";
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from "@/components/ui/dialog";
import { api } from "@/lib/api";
import { BulkProgressView } from "./BulkProgressView";
import { describePublicationOpError } from "./bulkErrors";
import { publicationsNeedingOverwriteConfirm } from "./bulkGating";
import type { PublicationListItem, PublicationStatusResponse } from "./types";
import { useBulkOperation, type BulkItemState } from "./useBulkOperation";
import { publicationsQueryKey } from "./usePublications";

interface BulkPublishDialogProps {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  publications: PublicationListItem[];
  /** Снять успешно опубликованные из выделения (упавшие/пропущенные остаются для повтора). */
  onRunComplete: (states: BulkItemState[]) => void;
}

function label(p: PublicationListItem): string {
  return `${p.infobaseName} — ${p.siteName}${p.virtualPath}`;
}

// MLC-046: массовая (пере)публикация через webinst. Единое подтверждение перезатирания:
// показываем явный список публикаций, созданных не панелью (Source ≠ Webinst) и уже
// опубликованных — webinst перезапишет их default.vrd/web.config. confirm=true идёт на
// все элементы (явное действие оператора). Пачка исполняется пулом (useBulkOperation).
export function BulkPublishDialog({
  open,
  onOpenChange,
  publications,
  onRunComplete,
}: BulkPublishDialogProps) {
  const { t } = useTranslation();
  const queryClient = useQueryClient();

  const gated = useMemo(() => publicationsNeedingOverwriteConfirm(publications), [publications]);

  const handleComplete = useCallback(
    (states: BulkItemState[]) => {
      void queryClient.invalidateQueries({ queryKey: publicationsQueryKey });
      onRunComplete(states);
    },
    [queryClient, onRunComplete]
  );

  const runItem = useCallback(
    (id: string) =>
      api<PublicationStatusResponse>(`/api/v1/publications/${id}/publish`, {
        method: "POST",
        body: { confirm: true },
      }).then(() => undefined),
    []
  );

  const describeError = useCallback((error: unknown) => describePublicationOpError(error, t), [t]);

  const { states, phase, summary, start, cancel, reset } = useBulkOperation({
    runItem,
    describeError,
    onComplete: handleComplete,
  });

  const handleOpenChange = (next: boolean) => {
    if (!next && phase === "running") return; // во время прогона не закрываем
    if (!next) reset();
    onOpenChange(next);
  };

  const handleStart = () => {
    void start(publications.map((p) => ({ id: p.id, label: label(p) })));
  };

  return (
    <Dialog open={open} onOpenChange={handleOpenChange}>
      <DialogContent showCloseButton={phase !== "running"}>
        <DialogHeader>
          <DialogTitle>{t("publications.bulk.publish.title")}</DialogTitle>
          <DialogDescription>
            {t("publications.bulk.publish.body", { count: publications.length })}
          </DialogDescription>
        </DialogHeader>

        {phase === "idle" ? (
          <div className="space-y-3">
            {gated.length > 0 && (
              <div className="border-destructive/40 bg-destructive/5 space-y-2 rounded-md border p-3 text-sm">
                <p className="font-medium">
                  {t("publications.bulk.publish.overwriteWarning", { count: gated.length })}
                </p>
                <ul className="max-h-40 list-disc space-y-0.5 overflow-y-auto pl-5 font-mono text-xs">
                  {gated.map((p) => (
                    <li key={p.id}>{label(p)}</li>
                  ))}
                </ul>
              </div>
            )}
          </div>
        ) : (
          <BulkProgressView states={states} summary={summary} isRunning={phase === "running"} />
        )}

        <DialogFooter>
          {phase === "idle" && (
            <>
              <Button variant="outline" onClick={() => handleOpenChange(false)}>
                {t("publications.bulk.cancel")}
              </Button>
              <Button onClick={handleStart} disabled={publications.length === 0}>
                {t("publications.bulk.publish.confirm", { count: publications.length })}
              </Button>
            </>
          )}
          {phase === "running" && (
            <Button variant="outline" onClick={cancel}>
              {t("publications.bulk.stop")}
            </Button>
          )}
          {phase === "done" && (
            <Button onClick={() => handleOpenChange(false)}>{t("publications.bulk.close")}</Button>
          )}
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}
