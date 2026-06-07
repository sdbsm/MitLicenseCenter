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
import { useEnableAdmin } from "./useAdmins";

interface EnableAdminDialogProps {
  admin: Admin | null;
  open: boolean;
  onOpenChange: (open: boolean) => void;
}

export function EnableAdminDialog({ admin, open, onOpenChange }: EnableAdminDialogProps) {
  const { t } = useTranslation();
  const enable = useEnableAdmin();

  if (!admin) {
    return null;
  }

  const handleConfirm = async () => {
    try {
      await enable.mutateAsync(admin.id);
      toast.success(t("admins.toasts.enabled", { name: admin.userName }));
      onOpenChange(false);
    } catch {
      toast.error(t("errors.generic"));
    }
  };

  return (
    <AlertDialog open={open} onOpenChange={onOpenChange}>
      <AlertDialogContent>
        <AlertDialogHeader>
          <AlertDialogTitle>{t("admins.enable.title")}</AlertDialogTitle>
          <AlertDialogDescription>
            {t("admins.enable.body", { name: admin.userName })}
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
            {enable.isPending ? t("common.loading") : t("admins.enable.confirm")}
          </AlertDialogAction>
        </AlertDialogFooter>
      </AlertDialogContent>
    </AlertDialog>
  );
}
