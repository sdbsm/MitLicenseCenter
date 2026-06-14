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
import { useStopRecording } from "./useRecordings";

interface StopRecordingDialogProps {
  recordingId: string | null;
  open: boolean;
  onOpenChange: (open: boolean) => void;
}

/** Подтверждение остановки идущей записи (Admin, 06_UI_DESIGN — деструктивное/необратимое
 *  действие подтверждается). После стопа запись остаётся в списке как «Остановлена». */
export function StopRecordingDialog({ recordingId, open, onOpenChange }: StopRecordingDialogProps) {
  const { t } = useTranslation();
  const stop = useStopRecording();

  if (!recordingId) {
    return null;
  }

  const handleConfirm = async () => {
    try {
      await stop.mutateAsync(recordingId);
      toast.success(t("performance.recording.toasts.stopped"));
      onOpenChange(false);
    } catch {
      toast.error(t("errors.generic"));
    }
  };

  return (
    <AlertDialog open={open} onOpenChange={onOpenChange}>
      <AlertDialogContent>
        <AlertDialogHeader>
          <AlertDialogTitle>{t("performance.recording.stop.title")}</AlertDialogTitle>
          <AlertDialogDescription>{t("performance.recording.stop.body")}</AlertDialogDescription>
        </AlertDialogHeader>
        <AlertDialogFooter>
          <AlertDialogCancel disabled={stop.isPending}>{t("common.cancel")}</AlertDialogCancel>
          {/* MLC-138/UX-26: остановка записи — деструктивное необратимое действие */}
          <AlertDialogAction
            variant="destructive"
            disabled={stop.isPending}
            onClick={(e) => {
              e.preventDefault();
              void handleConfirm();
            }}
          >
            {stop.isPending ? t("common.loading") : t("performance.recording.stop.confirm")}
          </AlertDialogAction>
        </AlertDialogFooter>
      </AlertDialogContent>
    </AlertDialog>
  );
}
