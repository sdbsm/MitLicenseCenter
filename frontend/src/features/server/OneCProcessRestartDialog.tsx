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
import { ApiError, readConflictBody } from "@/lib/api";
import { useRestartOneCProcess } from "./useOneCProcesses";

interface OneCProcessRestartDialogProps {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  pid: number;
}

/**
 * Диалог-подтверждение мягкого рестарта рабочего процесса 1С (rphost) по Pid (MLC-220,
 * ADR-56). Стиль ADR-45/55: «да/нет» без ручного ввода токена; в теле — предупреждение,
 * что рестарт разорвёт активные сеансы на этом процессе (у rac нет «restart process» →
 * рестарт = завершение ОС-процесса rphost, кластер 1С поднимает новый). confirm:true уходит
 * на BE (серверный Confirm-гейт). На 409 (Pid переиспользован / процесс не исчез за таймаут)
 * и 404 (Pid не в кластере) показывает detail / общий текст.
 */
export function OneCProcessRestartDialog({
  open,
  onOpenChange,
  pid,
}: OneCProcessRestartDialogProps) {
  const { t } = useTranslation();
  const mutation = useRestartOneCProcess();

  const handleConfirm = async () => {
    try {
      await mutation.mutateAsync(pid);
      toast.success(t("server.processes.restart.toasts.success"));
      onOpenChange(false);
    } catch (error) {
      if (error instanceof ApiError && error.status === 409) {
        const detail =
          readConflictBody(error)?.detail ?? t("server.processes.restart.toasts.failed");
        toast.error(detail);
        return;
      }
      toast.error(t("server.processes.restart.toasts.failed"));
    }
  };

  return (
    <AlertDialog open={open} onOpenChange={onOpenChange}>
      <AlertDialogContent>
        <AlertDialogHeader>
          <AlertDialogTitle>{t("server.processes.restart.dialog.title")}</AlertDialogTitle>
          <AlertDialogDescription>
            {t("server.processes.restart.dialog.body", { pid })}
          </AlertDialogDescription>
        </AlertDialogHeader>

        {/* Предупреждение о разрыве активных сеансов на процессе (разрушительная операция). */}
        <p className="text-status-danger text-sm">{t("server.processes.restart.dialog.warning")}</p>

        {mutation.isPending && (
          <p className="text-muted-foreground text-sm">
            {t("server.processes.restart.dialog.mayTakeLong")}
          </p>
        )}

        <AlertDialogFooter>
          <AlertDialogCancel disabled={mutation.isPending}>
            {t("server.processes.restart.dialog.cancel")}
          </AlertDialogCancel>
          <AlertDialogAction
            disabled={mutation.isPending}
            onClick={(e) => {
              e.preventDefault();
              void handleConfirm();
            }}
          >
            {mutation.isPending
              ? t("common.loading")
              : t("server.processes.restart.dialog.confirm")}
          </AlertDialogAction>
        </AlertDialogFooter>
      </AlertDialogContent>
    </AlertDialog>
  );
}
