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
import type { Admin } from "./types";
import { useDisableAdmin } from "./useAdmins";

interface DisableAdminDialogProps {
  admin: Admin | null;
  open: boolean;
  onOpenChange: (open: boolean) => void;
}

export function DisableAdminDialog({ admin, open, onOpenChange }: DisableAdminDialogProps) {
  const { t } = useTranslation();
  const disable = useDisableAdmin();

  if (!admin) {
    return null;
  }

  const handleConfirm = async () => {
    try {
      await disable.mutateAsync(admin.id);
      toast.success(t("admins.toasts.disabled", { name: admin.userName }));
      onOpenChange(false);
    } catch (error) {
      // Серверные guard'ы → понятный тост; диалог остаётся открытым.
      const messageKey = matchConflictCode(error, {
        ADMIN_CANNOT_DISABLE_SELF: "admins.errors.cannotDisableSelf",
        ADMIN_LAST_ACTIVE: "admins.errors.lastActive",
      });
      toast.error(messageKey ? t(messageKey) : t("errors.generic"));
    }
  };

  return (
    <AlertDialog open={open} onOpenChange={onOpenChange}>
      <AlertDialogContent>
        <AlertDialogHeader>
          <AlertDialogTitle>{t("admins.disable.title")}</AlertDialogTitle>
          <AlertDialogDescription>
            {t("admins.disable.body", { name: admin.userName })}
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
            {disable.isPending ? t("common.loading") : t("admins.disable.confirm")}
          </AlertDialogAction>
        </AlertDialogFooter>
      </AlertDialogContent>
    </AlertDialog>
  );
}
