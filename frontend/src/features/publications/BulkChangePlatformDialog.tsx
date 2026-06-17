import { useQueryClient } from "@tanstack/react-query";
import { useCallback, useState } from "react";
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
import { Label } from "@/components/ui/label";
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@/components/ui/select";
import { usePlatformVersions } from "@/features/discovery/useDiscovery";
import { api } from "@/lib/api";
import { BulkProgressView } from "./BulkProgressView";
import { describePublicationOpError } from "./bulkErrors";
import type { PublicationListItem, PublicationStatusResponse } from "./types";
import { useBulkOperation, type BulkItemState } from "./useBulkOperation";
import { infobasesQueryKey } from "@/features/infobases/useInfobases";

interface BulkChangePlatformDialogProps {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  publications: PublicationListItem[];
  onRunComplete: (states: BulkItemState[]) => void;
  // MLC-181c — сводка активного фильтра при выборе через «Выбрать все по фильтру».
  filterSummary?: string | null;
}

function label(p: PublicationListItem): string {
  return `${p.infobaseName} — ${p.siteName}${p.virtualPath}`;
}

// MLC-046: массовая смена платформы — одна целевая версия (из установленных) на всю
// выборку, правка пути к wsisapi.dll в web.config по каждой публикации. Смешанные
// текущие версии допустимы; «установлена» проверяет бэкенд по каждому элементу.
export function BulkChangePlatformDialog({
  open,
  onOpenChange,
  publications,
  onRunComplete,
  filterSummary,
}: BulkChangePlatformDialogProps) {
  const { t } = useTranslation();
  const queryClient = useQueryClient();
  const [version, setVersion] = useState("");
  const platforms = usePlatformVersions(open);
  const options = platforms.data?.items ?? [];

  const handleComplete = useCallback(
    (states: BulkItemState[]) => {
      void queryClient.invalidateQueries({ queryKey: infobasesQueryKey });
      onRunComplete(states);
    },
    [queryClient, onRunComplete]
  );

  const runItem = useCallback(
    (id: string) =>
      api<PublicationStatusResponse>(`/api/v1/publications/${id}/change-platform`, {
        method: "POST",
        body: { platformVersion: version },
      }).then(() => undefined),
    [version]
  );

  const describeError = useCallback((error: unknown) => describePublicationOpError(error, t), [t]);

  const { states, phase, summary, start, cancel, reset } = useBulkOperation({
    runItem,
    describeError,
    onComplete: handleComplete,
  });

  const handleOpenChange = (next: boolean) => {
    if (!next && phase === "running") return;
    if (!next) {
      reset();
      setVersion("");
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
          <DialogTitle>{t("publications.bulk.changePlatform.title")}</DialogTitle>
          <DialogDescription>
            {t("publications.bulk.changePlatform.body", { count: publications.length })}
          </DialogDescription>
        </DialogHeader>

        {phase === "idle" ? (
          <div className="grid gap-3">
            {filterSummary && (
              <div className="bg-muted/40 rounded-md border p-3 text-sm">
                <p className="font-medium">
                  {t("publications.bulk.filterSummaryLabel", { count: publications.length })}
                </p>
                <p className="text-muted-foreground mt-1">{filterSummary}</p>
              </div>
            )}
            <Label className="text-sm">{t("publications.changePlatform.versionLabel")}</Label>
            <Select value={version} onValueChange={setVersion}>
              <SelectTrigger>
                <SelectValue placeholder={t("publications.changePlatform.versionPlaceholder")} />
              </SelectTrigger>
              <SelectContent>
                {options.map((p) => (
                  <SelectItem key={p.version} value={p.version}>
                    {p.version}
                    {p.architecture ? ` (${p.architecture})` : ""}
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>
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
              <Button onClick={handleStart} disabled={!version || publications.length === 0}>
                {t("publications.bulk.changePlatform.confirm", { count: publications.length })}
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
