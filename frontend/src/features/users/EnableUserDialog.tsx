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
import { useEnableUser } from "./useUsers";

interface EnableUserDialogProps {
  user: User | null;
  open: boolean;
  onOpenChange: (open: boolean) => void;
}

export function EnableUserDialog({ user, open, onOpenChange }: EnableUserDialogProps) {
  const { t } = useTranslation();
  const enable = useEnableUser();

  if (!user) {
    return null;
  }

  const handleConfirm = async () => {
    try {
      await enable.mutateAsync(user.id);
      toast.success(t("users.toasts.enabled", { name: user.userName }));
      onOpenChange(false);
    } catch {
      toast.error(t("errors.generic"));
    }
  };

  return (
    <AlertDialog open={open} onOpenChange={onOpenChange}>
      <AlertDialogContent>
        <AlertDialogHeader>
          <AlertDialogTitle>{t("users.enable.title")}</AlertDialogTitle>
          <AlertDialogDescription>
            {t("users.enable.body", { name: user.userName })}
          </AlertDialogDescription>
        </AlertDialogHeader>
        <AlertDialogFooter>
          <AlertDialogCancel disabled={enable.isPending}>{t("common.cancel")}</AlertDialogCancel>
          <AlertDialogAction
            disabled={enable.isPending}
            onClick={(e) => {
              e.preventDefault();
              void handleConfirm();
            }}
          >
            {enable.isPending ? t("common.loading") : t("users.enable.confirm")}
          </AlertDialogAction>
        </AlertDialogFooter>
      </AlertDialogContent>
    </AlertDialog>
  );
}
