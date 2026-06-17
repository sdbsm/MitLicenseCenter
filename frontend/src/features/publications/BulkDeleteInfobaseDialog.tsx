import { useQueryClient } from "@tanstack/react-query";
import { useCallback, useMemo, useState } from "react";
import { useTranslation } from "react-i18next";
import { Button } from "@/components/ui/button";
import { Checkbox } from "@/components/ui/checkbox";
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from "@/components/ui/dialog";
import { Label } from "@/components/ui/label";
import { api } from "@/lib/api";
import { BulkProgressView } from "./BulkProgressView";
import { describePublicationOpError } from "./bulkErrors";
import type { PublicationListItem } from "./types";
import { useBulkOperation, type BulkItemState } from "./useBulkOperation";
import { infobasesQueryKey } from "@/features/infobases/useInfobases";

interface BulkDeleteInfobaseDialogProps {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  publications: PublicationListItem[];
  /** Снять успешно удалённые из выделения (упавшие/пропущенные остаются для повтора). */
  onRunComplete: (states: BulkItemState[]) => void;
  // MLC-181c — сводка активного фильтра при выборе через «Выбрать все по фильтру».
  filterSummary?: string | null;
}

function label(p: PublicationListItem): string {
  return `${p.infobaseName} — ${p.siteName}${p.virtualPath}`;
}

// MLC-184b: массовое удаление ИНФОБАЗ. НЕОБРАТИМО — по ADR-45 «да/нет» с явным
// предупреждением о необратимости (ключ переиспользован из DeleteInfobaseDialog), без
// type-to-confirm. Один общий чекбокс «Снять публикации из IIS» (дефолт ВЫКЛ): при
// включённом DELETE получает ?unpublishFromIis=true (как в useDeleteInfobase).
//
// ВАЖНО: операция бьёт по инфобазе (DELETE /api/v1/infobases/{infobaseId}), но движок bulk
// и deselectSucceeded оперируют id ПУБЛИКАЦИИ. Поэтому в пул отдаём publicationId
// (deselect остаётся корректным), а внутри runItem резолвим infobaseId через map
// publicationId → infobaseId по publications.
export function BulkDeleteInfobaseDialog({
  open,
  onOpenChange,
  publications,
  onRunComplete,
  filterSummary,
}: BulkDeleteInfobaseDialogProps) {
  const { t } = useTranslation();
  const queryClient = useQueryClient();
  const [unpublishFromIis, setUnpublishFromIis] = useState(false);

  // publicationId → infobaseId: пул работает по publicationId (для корректного deselect),
  // а DELETE — по infobaseId выбранной строки.
  const infobaseByPublicationId = useMemo(() => {
    const map = new Map<string, string>();
    for (const p of publications) map.set(p.id, p.infobaseId);
    return map;
  }, [publications]);

  const handleComplete = useCallback(
    (states: BulkItemState[]) => {
      void queryClient.invalidateQueries({ queryKey: infobasesQueryKey });
      onRunComplete(states);
    },
    [queryClient, onRunComplete]
  );

  const runItem = useCallback(
    (publicationId: string) => {
      const infobaseId = infobaseByPublicationId.get(publicationId);
      const query = unpublishFromIis ? "?unpublishFromIis=true" : "";
      return api<null>(`/api/v1/infobases/${infobaseId}${query}`, {
        method: "DELETE",
      }).then(() => undefined);
    },
    [infobaseByPublicationId, unpublishFromIis]
  );

  const describeError = useCallback((error: unknown) => describePublicationOpError(error, t), [t]);

  const { states, phase, summary, start, cancel, reset } = useBulkOperation({
    runItem,
    describeError,
    onComplete: handleComplete,
  });

  const handleOpenChange = (next: boolean) => {
    if (!next && phase === "running") return; // во время прогона не закрываем
    if (!next) {
      reset();
      setUnpublishFromIis(false);
    }
    onOpenChange(next);
  };

  const handleStart = () => {
    void start(publications.map((p) => ({ id: p.id, label: label(p) })));
  };

  return (
    <Dialog open={open} onOpenChange={handleOpenChange}>
      <DialogContent showCloseButton={phase !== "running"}>
        <DialogHeader>
          <DialogTitle>{t("publications.bulk.deleteInfobase.title")}</DialogTitle>
          <DialogDescription>
            {t("publications.bulk.deleteInfobase.body", { count: publications.length })}
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
            <p className="text-destructive text-sm font-medium">
              {t("publications.bulk.deleteInfobase.irreversibleWarning")}
            </p>
            <div className="flex items-start gap-2">
              <Checkbox
                id="bulk-delete-unpublish-from-iis"
                checked={unpublishFromIis}
                onCheckedChange={(v) => setUnpublishFromIis(v === true)}
              />
              <Label
                htmlFor="bulk-delete-unpublish-from-iis"
                className="text-muted-foreground text-sm leading-snug font-normal"
              >
                {t("publications.bulk.deleteInfobase.unpublishOption")}
              </Label>
            </div>
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
              <Button
                variant="destructive"
                onClick={handleStart}
                disabled={publications.length === 0}
              >
                {t("publications.bulk.deleteInfobase.confirm", { count: publications.length })}
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
