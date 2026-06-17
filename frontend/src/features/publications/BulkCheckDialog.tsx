import { useQueryClient } from "@tanstack/react-query";
import { useCallback } from "react";
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
import type { PublicationListItem, PublicationStatusResponse } from "./types";
import { useBulkOperation, type BulkItemState } from "./useBulkOperation";
import { infobasesQueryKey } from "@/features/infobases/useInfobases";

interface BulkCheckDialogProps {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  publications: PublicationListItem[];
  /** Снять успешно проверенные из выделения (упавшие/пропущенные остаются для повтора). */
  onRunComplete: (states: BulkItemState[]) => void;
  // MLC-181c — сводка активного фильтра при выборе через «Выбрать все по фильтру».
  filterSummary?: string | null;
}

function label(p: PublicationListItem): string {
  return `${p.infobaseName} — ${p.siteName}${p.virtualPath}`;
}

// MLC-184b: массовая проверка факта публикации в IIS. Read-only действие — без барьера
// подтверждения и без предупреждений: просто кнопка «Проверить N» + прогресс. Пачка
// исполняется пулом (useBulkOperation); по завершении инвалидируется список инфобаз.
export function BulkCheckDialog({
  open,
  onOpenChange,
  publications,
  onRunComplete,
  filterSummary,
}: BulkCheckDialogProps) {
  const { t } = useTranslation();
  const queryClient = useQueryClient();

  const handleComplete = useCallback(
    (states: BulkItemState[]) => {
      void queryClient.invalidateQueries({ queryKey: infobasesQueryKey });
      onRunComplete(states);
    },
    [queryClient, onRunComplete]
  );

  const runItem = useCallback(
    (id: string) =>
      api<PublicationStatusResponse>(`/api/v1/publications/${id}/check`, {
        method: "POST",
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
          <DialogTitle>{t("publications.bulk.check.title")}</DialogTitle>
          <DialogDescription>
            {t("publications.bulk.check.body", { count: publications.length })}
          </DialogDescription>
        </DialogHeader>

        {phase === "idle" ? (
          <div className="space-y-3">
            {filterSummary && (
              <div className="bg-muted/40 rounded-md border p-3 text-sm">
                <p className="font-medium">
                  {t("publications.bulk.filterSummaryLabel", { count: publications.length })}
                </p>
                <p className="text-muted-foreground mt-1">{filterSummary}</p>
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
                {t("publications.bulk.check.confirm", { count: publications.length })}
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
