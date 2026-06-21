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
import { useDeleteInvestigation } from "@/features/investigations/useInvestigations";

interface DeleteInvestigationDialogProps {
  investigationId: string | null;
  open: boolean;
  onOpenChange: (open: boolean) => void;
}

/**
 * Подтверждение удаления дела (Admin, необратимо — сносит и находки). Активное дело
 * бэкенд удалить не даёт (409 INVESTIGATION_ACTIVE) — переводим в понятный тост.
 */
export function DeleteInvestigationDialog({
  investigationId,
  open,
  onOpenChange,
}: DeleteInvestigationDialogProps) {
  const { t } = useTranslation();
  const remove = useDeleteInvestigation();

  if (!investigationId) return null;

  const handleConfirm = async () => {
    try {
      await remove.mutateAsync(investigationId);
      toast.success(t("common.deleted"));
      onOpenChange(false);
    } catch (error) {
      const conflictKey = matchConflictCode(error, {
        INVESTIGATION_ACTIVE: "investigations.list.delete.errorActive",
      });
      toast.error(conflictKey ? t(conflictKey) : t("errors.generic"));
    }
  };

  return (
    <AlertDialog open={open} onOpenChange={onOpenChange}>
      <AlertDialogContent>
        <AlertDialogHeader>
          <AlertDialogTitle>{t("investigations.list.delete.title")}</AlertDialogTitle>
          <AlertDialogDescription>{t("investigations.list.delete.body")}</AlertDialogDescription>
        </AlertDialogHeader>
        <AlertDialogFooter>
          <AlertDialogCancel disabled={remove.isPending}>{t("common.cancel")}</AlertDialogCancel>
          <AlertDialogAction
            disabled={remove.isPending}
            variant="destructive"
            onClick={(e) => {
              e.preventDefault();
              void handleConfirm();
            }}
          >
            {remove.isPending ? t("common.loading") : t("investigations.list.delete.confirm")}
          </AlertDialogAction>
        </AlertDialogFooter>
      </AlertDialogContent>
    </AlertDialog>
  );
}
