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
import { useOneCServerOperation, type OneCServerOperation } from "./useServerStatus";

interface OneCServerActionDialogProps {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  // Только разрушительные операции идут через диалог: stop / restart (ADR-55).
  operation: Exclude<OneCServerOperation, "start">;
  serviceName: string;
}

// Диалог-подтверждение остановки/перезапуска сервера 1С (MLC-214, ADR-55). Стиль
// ADR-45/55: «да/нет» без ручного ввода токена; в теле — предупреждение, что
// операция прервёт работу всех баз узла. confirm:true уходит на BE (серверный
// Confirm-гейт). На 409 показывает detail (санитизированный текст BE).
export function OneCServerActionDialog({
  open,
  onOpenChange,
  operation,
  serviceName,
}: OneCServerActionDialogProps) {
  const { t } = useTranslation();
  const mutation = useOneCServerOperation();

  const handleConfirm = async () => {
    try {
      await mutation.mutateAsync({ operation, serviceName, confirm: true });
      toast.success(t(`server.toasts.${operation}`));
      onOpenChange(false);
    } catch (error) {
      if (error instanceof ApiError && error.status === 409) {
        const detail = readConflictBody(error)?.detail ?? t("server.toasts.failed");
        toast.error(detail);
        return;
      }
      toast.error(t("errors.generic"));
    }
  };

  return (
    <AlertDialog open={open} onOpenChange={onOpenChange}>
      <AlertDialogContent>
        <AlertDialogHeader>
          <AlertDialogTitle>{t(`server.dialog.${operation}.title`)}</AlertDialogTitle>
          <AlertDialogDescription>
            {t(`server.dialog.${operation}.body`, { serviceName })}
          </AlertDialogDescription>
        </AlertDialogHeader>

        {/* Предупреждение о простое всех баз узла (разрушительная операция). */}
        <p className="text-status-danger text-sm">{t("server.dialog.disruptionWarning")}</p>

        {mutation.isPending && (
          <p className="text-muted-foreground text-sm">{t("server.dialog.mayTakeLong")}</p>
        )}

        <AlertDialogFooter>
          <AlertDialogCancel disabled={mutation.isPending}>
            {t("server.dialog.cancel")}
          </AlertDialogCancel>
          <AlertDialogAction
            disabled={mutation.isPending}
            onClick={(e) => {
              e.preventDefault();
              void handleConfirm();
            }}
          >
            {mutation.isPending ? t("common.loading") : t(`server.dialog.${operation}.confirm`)}
          </AlertDialogAction>
        </AlertDialogFooter>
      </AlertDialogContent>
    </AlertDialog>
  );
}
