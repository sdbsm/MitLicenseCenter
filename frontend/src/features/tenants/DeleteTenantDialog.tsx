import { useRef, useState } from "react";
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
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { matchConflictCode } from "@/lib/apiErrors";
import type { Tenant } from "./types";
import { useDeleteTenant } from "./useTenants";

interface DeleteTenantDialogProps {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  tenant: Tenant | null;
}

export function DeleteTenantDialog({ open, onOpenChange, tenant }: DeleteTenantDialogProps) {
  const { t } = useTranslation();
  const cancelRef = useRef<HTMLButtonElement>(null);
  const [confirmation, setConfirmation] = useState("");
  const remove = useDeleteTenant();

  if (!tenant) {
    return null;
  }

  const matched = confirmation.trim() === tenant.name;

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

        <div className="grid gap-2">
          <Label htmlFor="confirm-tenant-name" className="text-sm">
            {t("tenants.delete.confirmLabel", { name: tenant.name })}
          </Label>
          <Input
            id="confirm-tenant-name"
            autoComplete="off"
            value={confirmation}
            onChange={(e) => setConfirmation(e.target.value)}
            placeholder={tenant.name}
          />
        </div>

        <AlertDialogFooter>
          <AlertDialogCancel ref={cancelRef} disabled={remove.isPending}>
            {t("common.cancel")}
          </AlertDialogCancel>
          <AlertDialogAction
            disabled={!matched || remove.isPending}
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
