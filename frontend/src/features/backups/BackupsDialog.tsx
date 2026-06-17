import { format } from "date-fns";
import { ru } from "date-fns/locale";
import {
  ArchiveIcon,
  CircleIcon,
  DatabaseBackupIcon,
  Trash2Icon,
  TriangleAlertIcon,
} from "lucide-react";
import { useState } from "react";
import { useTranslation } from "react-i18next";
import { toast } from "sonner";
import { Button } from "@/components/ui/button";
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogHeader,
  DialogTitle,
} from "@/components/ui/dialog";
import { Skeleton } from "@/components/ui/skeleton";
import { StatusBadge } from "@/components/ui/StatusBadge";
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from "@/components/ui/table";
import { useMe } from "@/features/auth/useAuth";
import type { InfobaseListItem } from "@/features/infobases/types";
import { matchConflictCode } from "@/lib/apiErrors";
import { backupStatusVariant, formatBackupSize } from "./backupFormat";
import { DeleteBackupDialog } from "./DeleteBackupDialog";
import type { BackupEstimate, BackupSummary } from "./types";
import { useBackupEstimate, useBackups, useStartBackup } from "./useBackups";

interface BackupsDialogProps {
  infobase: InfobaseListItem | null;
  open: boolean;
  onOpenChange: (open: boolean) => void;
}

function fmt(iso: string | null): string {
  return iso ? format(new Date(iso), "dd.MM.yyyy HH:mm", { locale: ru }) : "—";
}

/**
 * Бэкапы одной инфобазы (MLC-078, ADR-27) — диалог с карточки/строки инфобазы. Запуск
 * on-demand `COPY_ONLY`-бэкапа доступен и Viewer (операторская кнопка), удаление — только
 * Admin (гейт по `useMe`, образец RecordingSection). Список поллится каждые 5с, пока диалог
 * открыт, — статусы двигает оркестратор на бэкенде (очередь → выполнение → итог). Кнопка
 * запуска НЕ блокируется при активном бэкапе: дубль честно отвечает 409 BACKUP_ACTIVE
 * (серверный замок-на-базу — единственный источник правды, без гонок UI).
 */
export function BackupsDialog({ infobase, open, onOpenChange }: BackupsDialogProps) {
  const { t } = useTranslation();
  const { data: me } = useMe();
  const isAdmin = me?.roles?.includes("Admin") ?? false;

  const { data, isLoading, isError } = useBackups(open ? (infobase?.id ?? null) : null);
  const estimate = useBackupEstimate(open ? (infobase?.id ?? null) : null);
  const start = useStartBackup();
  const [deleting, setDeleting] = useState<BackupSummary | null>(null);

  if (!infobase) {
    return null;
  }

  const backups = data?.items ?? [];

  const handleStart = async () => {
    try {
      await start.mutateAsync(infobase.id);
      toast.success(t("backups.toasts.queued"));
    } catch (error) {
      const messageKey = matchConflictCode(error, {
        BACKUP_ACTIVE: "backups.errors.active",
        BACKUP_FOLDER_NOT_CONFIGURED: "backups.errors.folderNotConfigured",
      });
      toast.error(messageKey ? t(messageKey) : t("errors.generic"));
    }
  };

  return (
    <>
      <Dialog open={open} onOpenChange={onOpenChange}>
        <DialogContent className="max-h-[90vh] overflow-y-auto sm:max-w-3xl">
          <DialogHeader className="pr-8">
            <div className="flex flex-wrap items-center justify-between gap-3">
              <DialogTitle>{t("backups.title", { name: infobase.name })}</DialogTitle>
              <Button
                size="sm"
                disabled={start.isPending}
                onClick={() => {
                  void handleStart();
                }}
              >
                <DatabaseBackupIcon className="size-4" />
                {t("backups.actions.start")}
              </Button>
            </div>
            <DialogDescription>
              {t("backups.subtitle", { db: infobase.databaseName })}
            </DialogDescription>
            <EstimateLine isLoading={estimate.isLoading} estimate={estimate.data ?? null} />
          </DialogHeader>

          {isError && !data && (
            <p className="text-muted-foreground text-sm">{t("backups.errors.loadFailed")}</p>
          )}

          {isLoading && !data ? (
            <Skeleton className="h-32 w-full" />
          ) : backups.length === 0 ? (
            <div className="flex flex-col items-center justify-center gap-3 py-10 text-center">
              <ArchiveIcon className="text-muted-foreground size-8" />
              <div className="space-y-1">
                <p className="font-medium">{t("backups.empty.title")}</p>
                <p className="text-muted-foreground text-sm">{t("backups.empty.hint")}</p>
              </div>
            </div>
          ) : (
            <div className="rounded-md border">
              <Table>
                <TableHeader>
                  <TableRow>
                    <TableHead>{t("backups.fields.status")}</TableHead>
                    <TableHead>{t("backups.fields.requested")}</TableHead>
                    <TableHead>{t("backups.fields.completed")}</TableHead>
                    <TableHead className="text-right">{t("backups.fields.size")}</TableHead>
                    <TableHead>{t("backups.fields.requestedBy")}</TableHead>
                    {isAdmin && <TableHead className="w-10" />}
                  </TableRow>
                </TableHeader>
                <TableBody>
                  {backups.map((b) => (
                    <TableRow key={b.id}>
                      <TableCell>
                        <StatusBadge variant={backupStatusVariant(b.status)}>
                          {b.status === "Running" && (
                            <CircleIcon className="size-2 animate-pulse fill-current" />
                          )}
                          {t(`backups.status.${b.status}`, { defaultValue: b.status })}
                        </StatusBadge>
                        {b.status === "Failed" && b.failureReason !== "None" && (
                          <p className="text-destructive mt-1 text-xs">
                            {t(`backups.failureReason.${b.failureReason}`, {
                              defaultValue: b.failureReason,
                            })}
                          </p>
                        )}
                        {b.errorMessage && (
                          <p
                            className="text-muted-foreground mt-1 max-w-64 truncate text-xs"
                            title={b.errorMessage}
                          >
                            {b.errorMessage}
                          </p>
                        )}
                      </TableCell>
                      <TableCell className="text-muted-foreground tabular-nums">
                        {fmt(b.requestedAtUtc)}
                      </TableCell>
                      <TableCell className="text-muted-foreground tabular-nums">
                        {fmt(b.completedAtUtc)}
                      </TableCell>
                      <TableCell className="text-right tabular-nums">
                        {b.status === "Succeeded" && b.fileAvailable === false ? (
                          // MLC-178/182: строка ссылается на файл, но на диске его нет
                          // (удалён вручную/внешней чисткой) — заметный сигнал, не норма.
                          <span className="text-destructive" title={b.filePath ?? undefined}>
                            {t("backups.fileMissing")}
                          </span>
                        ) : b.status === "Succeeded" && !b.filePath ? (
                          // MLC-182: файл вытеснен keep-latest (хранится только последний) —
                          // это норма, спокойная пометка вместо немого «—».
                          <span
                            className="text-muted-foreground"
                            title={t("backups.fileSupersededHint")}
                          >
                            {t("backups.fileSuperseded")}
                          </span>
                        ) : b.filePath ? (
                          <span title={b.filePath}>{formatBackupSize(b.fileSizeBytes)}</span>
                        ) : (
                          formatBackupSize(b.fileSizeBytes)
                        )}
                      </TableCell>
                      <TableCell className="text-muted-foreground">{b.requestedBy}</TableCell>
                      {isAdmin && (
                        <TableCell className="text-right">
                          <Button
                            variant="ghost"
                            size="icon"
                            className="size-8"
                            disabled={b.status === "Running"}
                            onClick={() => setDeleting(b)}
                          >
                            <Trash2Icon className="size-4" />
                            <span className="sr-only">{t("backups.actions.delete")}</span>
                          </Button>
                        </TableCell>
                      )}
                    </TableRow>
                  ))}
                </TableBody>
              </Table>
            </div>
          )}
        </DialogContent>
      </Dialog>

      <DeleteBackupDialog
        backup={deleting}
        open={deleting !== null}
        onOpenChange={(o) => {
          if (!o) setDeleting(null);
        }}
      />
    </>
  );
}

/**
 * Предпоказ disk-guard (MLC-183): «Свободно на диске … · Оценка бэкапа ~… (до сжатия)». Состояния:
 * - обе цифры есть, `sufficient` → спокойный muted;
 * - обе цифры есть, `!sufficient` → заметное предупреждение (destructive + иконка), но кнопку
 *   «Сделать бэкап» НЕ блокируем (оценка — верхняя граница; серверный disk-guard остаётся
 *   стоп-краном);
 * - цифры недоступны (degraded: нет чисел / `!folderConfigured` / `reason==='PermissionDenied'`)
 *   → нейтральное «оценка недоступна» (причина в `title`);
 * - loading → лёгкий плейсхолдер в одну строку (список не блокируем).
 */
function EstimateLine({
  isLoading,
  estimate,
}: {
  isLoading: boolean;
  estimate: BackupEstimate | null;
}) {
  const { t } = useTranslation();

  if (isLoading && !estimate) {
    return <Skeleton className="mt-1 h-4 w-64" />;
  }

  const hasNumbers =
    estimate != null &&
    estimate.folderConfigured &&
    estimate.estimatedSizeBytes != null &&
    estimate.freeSpaceBytes != null &&
    estimate.reason !== "PermissionDenied";

  if (!hasNumbers) {
    return (
      <p className="text-muted-foreground mt-1 text-xs" title={estimate?.reason ?? undefined}>
        {t("backups.estimate.unavailable")}
      </p>
    );
  }

  const free = formatBackupSize(estimate.freeSpaceBytes);
  const size = formatBackupSize(estimate.estimatedSizeBytes);
  const summary = `${t("backups.estimate.free", { free })} · ${t("backups.estimate.size", {
    size,
  })} (${t("backups.estimate.beforeCompression")})`;

  if (!estimate.sufficient) {
    return (
      <p className="text-destructive mt-1 flex items-center gap-1.5 text-xs">
        <TriangleAlertIcon className="size-3.5 shrink-0" />
        <span>
          {summary} — {t("backups.estimate.insufficient")}
        </span>
      </p>
    );
  }

  return <p className="text-muted-foreground mt-1 text-xs">{summary}</p>;
}
