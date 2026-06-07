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
import { useDisableUser } from "./useUsers";

interface DisableUserDialogProps {
  user: User | null;
  open: boolean;
  onOpenChange: (open: boolean) => void;
}

export function DisableUserDialog({ user, open, onOpenChange }: DisableUserDialogProps) {
  const { t } = useTranslation();
  const disable = useDisableUser();

  if (!user) {
    return null;
  }

  const handleConfirm = async () => {
    try {
      await disable.mutateAsync(user.id);
      toast.success(t("users.toasts.disabled", { name: user.userName }));
      onOpenChange(false);
    } catch (error) {
      // Серверные guard'ы → понятный тост; диалог остаётся открытым.
      const messageKey = matchConflictCode(error, {
        USER_CANNOT_DISABLE_SELF: "users.errors.cannotDisableSelf",
        USER_LAST_ACTIVE: "users.errors.lastActive",
      });
      toast.error(messageKey ? t(messageKey) : t("errors.generic"));
    }
  };

  return (
    <AlertDialog open={open} onOpenChange={onOpenChange}>
      <AlertDialogContent>
        <AlertDialogHeader>
          <AlertDialogTitle>{t("users.disable.title")}</AlertDialogTitle>
          <AlertDialogDescription>
            {t("users.disable.body", { name: user.userName })}
          </AlertDialogDescription>
        </AlertDialogHeader>
        <AlertDialogFooter>
          <AlertDialogCancel disabled={disable.isPending}>{t("common.cancel")}</AlertDialogCancel>
          <AlertDialogAction
            disabled={disable.isPending}
            className="bg-destructive text-destructive-foreground hover:bg-destructive/90 focus-visible:ring-destructive/20"
            onClick={(e) => {
              e.preventDefault();
              void handleConfirm();
            }}
          >
            {disable.isPending ? t("common.loading") : t("users.disable.confirm")}
          </AlertDialogAction>
        </AlertDialogFooter>
      </AlertDialogContent>
    </AlertDialog>
  );
}
