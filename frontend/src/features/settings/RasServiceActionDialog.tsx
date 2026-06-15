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
import {
  useRasServiceOperation,
  type RasServiceOperation,
  type RasServiceStatus,
} from "./useRasService";

interface RasServiceActionDialogProps {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  operation: RasServiceOperation;
  status: RasServiceStatus;
}

// Диалог-подтверждение лечащего действия со службой RAS (MLC-160, ADR-47). Стиль
// ADR-45: действие обратимо (register/update/start) → простое «да/нет» без ручного
// ввода токена. Показывает И человеческое описание (служба / платформа / порт из
// target), И точную команду sc … (commandPreview) — прозрачность + воспроизводимость
// оператором вручную. На 409 показывает detail (санитизированный текст BE).
export function RasServiceActionDialog({
  open,
  onOpenChange,
  operation,
  status,
}: RasServiceActionDialogProps) {
  const { t } = useTranslation();
  const mutation = useRasServiceOperation();

  const target = status.target;

  const handleConfirm = async () => {
    try {
      await mutation.mutateAsync(operation);
      toast.success(t(`settings.rasService.toasts.${operation}`));
      onOpenChange(false);
    } catch (error) {
      if (error instanceof ApiError && error.status === 409) {
        const detail = readConflictBody(error)?.detail ?? t("settings.rasService.toasts.failed");
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
          <AlertDialogTitle>{t(`settings.rasService.dialog.${operation}.title`)}</AlertDialogTitle>
          <AlertDialogDescription>
            {t(`settings.rasService.dialog.${operation}.body`)}
          </AlertDialogDescription>
        </AlertDialogHeader>

        {/* Человеческое описание целевых параметров службы. */}
        {target && (
          <dl className="grid grid-cols-[auto_1fr] gap-x-4 gap-y-1 text-sm">
            <dt className="text-muted-foreground">{t("settings.rasService.fields.platform")}</dt>
            <dd className="font-medium">{target.platformVersion}</dd>
            <dt className="text-muted-foreground">{t("settings.rasService.fields.port")}</dt>
            <dd className="font-medium">{target.port}</dd>
            <dt className="text-muted-foreground">{t("settings.rasService.fields.rasExe")}</dt>
            <dd className="font-medium break-all">{target.rasExePath}</dd>
          </dl>
        )}

        {/* Точная команда sc … (прозрачность ADR-47). */}
        {status.commandPreview && (
          <div className="grid gap-1">
            <p className="text-muted-foreground text-xs">
              {t("settings.rasService.fields.command")}
            </p>
            <code className="bg-muted block overflow-x-auto rounded-md p-2 font-mono text-xs">
              {status.commandPreview}
            </code>
          </div>
        )}

        <AlertDialogFooter>
          <AlertDialogCancel disabled={mutation.isPending}>
            {t("settings.rasService.dialog.cancel")}
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
              : t(`settings.rasService.dialog.${operation}.confirm`)}
          </AlertDialogAction>
        </AlertDialogFooter>
      </AlertDialogContent>
    </AlertDialog>
  );
}
