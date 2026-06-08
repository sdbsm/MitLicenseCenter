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
import type { User } from "./types";
import { useResetUserPassword } from "./useUsers";

interface ResetPasswordDialogProps {
  user: User | null;
  open: boolean;
  onOpenChange: (open: boolean) => void;
  onPasswordGenerated: (userName: string, password: string) => void;
}

export function ResetPasswordDialog({
  user,
  open,
  onOpenChange,
  onPasswordGenerated,
}: ResetPasswordDialogProps) {
  const { t } = useTranslation();
  const reset = useResetUserPassword();

  if (!user) {
    return null;
  }

  const handleConfirm = async () => {
    try {
      const result = await reset.mutateAsync(user.id);
      toast.success(t("users.toasts.passwordReset", { name: user.userName }));
      onOpenChange(false);
      onPasswordGenerated(user.userName, result.generatedPassword);
    } catch {
      toast.error(t("errors.generic"));
    }
  };

  return (
    <AlertDialog open={open} onOpenChange={onOpenChange}>
      <AlertDialogContent>
        <AlertDialogHeader>
          <AlertDialogTitle>{t("users.reset.title")}</AlertDialogTitle>
          <AlertDialogDescription>
            {t("users.reset.body", { name: user.userName })}
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
            {reset.isPending ? t("common.loading") : t("users.reset.confirm")}
          </AlertDialogAction>
        </AlertDialogFooter>
      </AlertDialogContent>
    </AlertDialog>
  );
}
