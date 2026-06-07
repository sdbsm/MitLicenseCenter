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
import type { Admin } from "./types";
import { useResetAdminPassword } from "./useAdmins";

interface ResetPasswordDialogProps {
  admin: Admin | null;
  open: boolean;
  onOpenChange: (open: boolean) => void;
  onPasswordGenerated: (userName: string, password: string) => void;
}

export function ResetPasswordDialog({
  admin,
  open,
  onOpenChange,
  onPasswordGenerated,
}: ResetPasswordDialogProps) {
  const { t } = useTranslation();
  const reset = useResetAdminPassword();

  if (!admin) {
    return null;
  }

  const handleConfirm = async () => {
    try {
      const result = await reset.mutateAsync(admin.id);
      toast.success(t("admins.toasts.passwordReset", { name: admin.userName }));
      onOpenChange(false);
      onPasswordGenerated(admin.userName, result.generatedPassword);
    } catch {
      toast.error(t("errors.generic"));
    }
  };

  return (
    <AlertDialog open={open} onOpenChange={onOpenChange}>
      <AlertDialogContent>
        <AlertDialogHeader>
          <AlertDialogTitle>{t("admins.reset.title")}</AlertDialogTitle>
          <AlertDialogDescription>
            {t("admins.reset.body", { name: admin.userName })}
          </AlertDialogDescription>
        </AlertDialogHeader>
        <AlertDialogFooter>
          <AlertDialogCancel disabled={reset.isPending}>{t("common.cancel")}</AlertDialogCancel>
          <AlertDialogAction
            disabled={reset.isPending}
            onClick={(e) => {
              e.preventDefault();
              void handleConfirm();
            }}
          >
            {reset.isPending ? t("common.loading") : t("admins.reset.confirm")}
          </AlertDialogAction>
        </AlertDialogFooter>
      </AlertDialogContent>
    </AlertDialog>
  );
}
