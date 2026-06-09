import { useTranslation } from "react-i18next";
import { toast } from "sonner";
import {
  AlertDialog,
  AlertDialogAction,
  AlertDialogCancel,
  AlertDialogContent,
  AlertDialogDescription,
  AlertDialogFooter,
  AlertDialogHeader,
  AlertDialogTitle,
} from "@/components/ui/alert-dialog";
import { matchConflictCode } from "@/lib/apiErrors";
import type { BackupSummary } from "./types";
import { useDeleteBackup } from "./useBackups";

interface DeleteBackupDialogProps {
  backup: BackupSummary | null;
  open: boolean;
  onOpenChange: (open: boolean) => void;
}

/** Подтверждение удаления бэкапа (Admin, необратимо — файл сносится с диска SQL-сервера).
 *  Running бэкенд удалить не даёт (409 BACKUP_ACTIVE); если файл не удалился, запись
 *  сохраняется (409 BACKUP_DELETE_FAILED) — оба случая переводим в понятные тосты. */
export function DeleteBackupDialog({ backup, open, onOpenChange }: DeleteBackupDialogProps) {
  const { t } = useTranslation();
  const remove = useDeleteBackup();

  if (!backup) {
    return null;
  }

  const handleConfirm = async () => {
    try {
      await remove.mutateAsync({ id: backup.id, infobaseId: backup.infobaseId });
      toast.success(t("backups.toasts.deleted"));
      onOpenChange(false);
    } catch (error) {
      const messageKey = matchConflictCode(error, {
        BACKUP_ACTIVE: "backups.errors.active",
        BACKUP_DELETE_FAILED: "backups.errors.deleteFailed",
      });
      toast.error(messageKey ? t(messageKey) : t("errors.generic"));
    }
  };

  return (
    <AlertDialog open={open} onOpenChange={onOpenChange}>
      <AlertDialogContent>
        <AlertDialogHeader>
          <AlertDialogTitle>{t("backups.delete.title")}</AlertDialogTitle>
          <AlertDialogDescription>
            {t("backups.delete.body", { name: backup.databaseName })}
          </AlertDialogDescription>
        </AlertDialogHeader>
        <AlertDialogFooter>
          <AlertDialogCancel disabled={remove.isPending}>{t("common.cancel")}</AlertDialogCancel>
          <AlertDialogAction
            disabled={remove.isPending}
            className="bg-destructive text-destructive-foreground hover:bg-destructive/90 focus-visible:ring-destructive/20"
            onClick={(e) => {
              e.preventDefault();
              void handleConfirm();
            }}
          >
            {remove.isPending ? t("common.loading") : t("backups.delete.confirm")}
          </AlertDialogAction>
        </AlertDialogFooter>
      </AlertDialogContent>
    </AlertDialog>
  );
}
