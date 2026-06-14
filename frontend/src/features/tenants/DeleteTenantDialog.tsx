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
import type { Tenant } from "./types";
import { useDeleteTenant } from "./useTenants";

// ADR-45: необратимое действие — «да/нет» с явным предупреждением; ручной ввод убран.
interface DeleteTenantDialogProps {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  tenant: Tenant | null;
}

export function DeleteTenantDialog({ open, onOpenChange, tenant }: DeleteTenantDialogProps) {
  const { t } = useTranslation();
  const remove = useDeleteTenant();

  if (!tenant) {
    return null;
  }

  const handleConfirm = async () => {
    try {
      await remove.mutateAsync(tenant.id);
      toast.success(t("tenants.toasts.deleted", { name: tenant.name }));
      onOpenChange(false);
    } catch (error) {
      const messageKey = matchConflictCode(error, {
        TENANT_HAS_INFOBASES: "tenants.errors.hasInfobases",
      });
      if (messageKey) {
        toast.error(t(messageKey));
        return;
      }
      toast.error(t("errors.generic"));
    }
  };

  return (
    <AlertDialog open={open} onOpenChange={onOpenChange}>
      <AlertDialogContent>
        <AlertDialogHeader>
          <AlertDialogTitle>{t("tenants.delete.title")}</AlertDialogTitle>
          <AlertDialogDescription>
            {t("tenants.delete.body", { name: tenant.name })}
          </AlertDialogDescription>
        </AlertDialogHeader>

        <p className="text-destructive text-sm font-medium">
          {t("tenants.delete.irreversibleWarning")}
        </p>

        <AlertDialogFooter>
          <AlertDialogCancel disabled={remove.isPending}>{t("common.cancel")}</AlertDialogCancel>
          <AlertDialogAction
            disabled={remove.isPending}
            className="bg-destructive text-destructive-foreground hover:bg-destructive/90 focus-visible:ring-destructive/20"
            onClick={(e) => {
              e.preventDefault();
              void handleConfirm();
            }}
          >
            {remove.isPending ? t("common.loading") : t("tenants.delete.confirm")}
          </AlertDialogAction>
        </AlertDialogFooter>
      </AlertDialogContent>
    </AlertDialog>
  );
}
