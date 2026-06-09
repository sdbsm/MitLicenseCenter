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
import { useDeleteRecording } from "./useRecordings";

interface DeleteRecordingDialogProps {
  recordingId: string | null;
  open: boolean;
  onOpenChange: (open: boolean) => void;
}

/** Подтверждение удаления записи (Admin, необратимо — сносит и сэмплы каскадом). Идущую запись
 *  бэкенд удалить не даёт (409 RECORDING_ACTIVE) — переводим в понятный тост «сначала остановите». */
export function DeleteRecordingDialog({
  recordingId,
  open,
  onOpenChange,
}: DeleteRecordingDialogProps) {
  const { t } = useTranslation();
  const remove = useDeleteRecording();

  if (!recordingId) {
    return null;
  }

  const handleConfirm = async () => {
    try {
      await remove.mutateAsync(recordingId);
      toast.success(t("performance.recording.toasts.deleted"));
      onOpenChange(false);
    } catch (error) {
      const messageKey = matchConflictCode(error, {
        RECORDING_ACTIVE: "performance.recording.errors.active",
      });
      toast.error(messageKey ? t(messageKey) : t("errors.generic"));
    }
  };

  return (
    <AlertDialog open={open} onOpenChange={onOpenChange}>
      <AlertDialogContent>
        <AlertDialogHeader>
          <AlertDialogTitle>{t("performance.recording.delete.title")}</AlertDialogTitle>
          <AlertDialogDescription>{t("performance.recording.delete.body")}</AlertDialogDescription>
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
            {remove.isPending ? t("common.loading") : t("performance.recording.delete.confirm")}
          </AlertDialogAction>
        </AlertDialogFooter>
      </AlertDialogContent>
    </AlertDialog>
  );
}
