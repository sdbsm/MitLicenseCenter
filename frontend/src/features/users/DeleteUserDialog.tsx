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
import type { User } from "./types";
import { useDeleteUser } from "./useUsers";

// MLC-180 — жёсткое удаление учётки. ADR-45: необратимое действие — «да/нет» с явным
// предупреждением, без поля ввода имени. Серверные guard'ы (self / последний активный
// Admin) переиспользуют коды отключения, но текст ошибок — delete-специфичный.
interface DeleteUserDialogProps {
  user: User | null;
  open: boolean;
  onOpenChange: (open: boolean) => void;
}

export function DeleteUserDialog({ user, open, onOpenChange }: DeleteUserDialogProps) {
  const { t } = useTranslation();
  const remove = useDeleteUser();

  if (!user) {
    return null;
  }

  const handleConfirm = async () => {
    try {
      await remove.mutateAsync(user.id);
      toast.success(t("users.toasts.deleted", { name: user.userName }));
      onOpenChange(false);
    } catch (error) {
      // Серверные guard'ы → понятный delete-специфичный тост; диалог остаётся открытым.
      const messageKey = matchConflictCode(error, {
        USER_CANNOT_DISABLE_SELF: "users.delete.errors.cannotDeleteSelf",
        USER_LAST_ACTIVE: "users.delete.errors.lastActive",
      });
      toast.error(messageKey ? t(messageKey) : t("errors.generic"));
    }
  };

  return (
    <AlertDialog open={open} onOpenChange={onOpenChange}>
      <AlertDialogContent>
        <AlertDialogHeader>
          <AlertDialogTitle>{t("users.delete.title")}</AlertDialogTitle>
          <AlertDialogDescription>
            {t("users.delete.body", { name: user.userName })}
          </AlertDialogDescription>
        </AlertDialogHeader>

        <p className="text-destructive text-sm font-medium">
          {t("users.delete.irreversibleWarning")}
        </p>

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
            {remove.isPending ? t("common.loading") : t("users.delete.confirm")}
          </AlertDialogAction>
        </AlertDialogFooter>
      </AlertDialogContent>
    </AlertDialog>
  );
}
