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
import { ApiError } from "@/lib/api";
import type { PublicationListItem } from "./types";
import { useReconcile } from "./usePublications";

interface ConflictBody {
  code?: string;
  detail?: string;
  title?: string;
}

interface ReconcilePublicationDialogProps {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  publication: PublicationListItem | null;
}

export function ReconcilePublicationDialog({
  open,
  onOpenChange,
  publication,
}: ReconcilePublicationDialogProps) {
  const { t } = useTranslation();
  const cancelRef = useRef<HTMLButtonElement>(null);
  const [confirmation, setConfirmation] = useState("");
  const reconcile = useReconcile();

  if (!publication) {
    return null;
  }

  const token = `${publication.siteName}${publication.virtualPath}`;
  const matched = confirmation.trim() === token;

  const handleConfirm = async () => {
    try {
      await reconcile.mutateAsync(publication.id);
      toast.success(t("publications.toasts.reconciled"));
      onOpenChange(false);
    } catch (error) {
      if (error instanceof ApiError && error.status === 409) {
        const body = error.body as ConflictBody | null;
        const detail = body?.detail ?? body?.title ?? t("publications.toasts.reconcileFailed");
        toast.error(detail);
        return;
      }
      toast.error(t("errors.generic"));
    }
  };

  return (
    <AlertDialog open={open} onOpenChange={onOpenChange}>
      <AlertDialogContent>
        <AlertDialogHeader>
          <AlertDialogTitle>{t("publications.reconcile.title")}</AlertDialogTitle>
          <AlertDialogDescription>
            {t("publications.reconcile.body", {
              siteName: publication.siteName,
              virtualPath: publication.virtualPath,
            })}
          </AlertDialogDescription>
        </AlertDialogHeader>

        <div className="grid gap-2">
          <Label htmlFor="confirm-reconcile-token" className="text-sm">
            {t("publications.reconcile.confirmLabel", { token })}
          </Label>
          <Input
            id="confirm-reconcile-token"
            autoComplete="off"
            value={confirmation}
            onChange={(e) => setConfirmation(e.target.value)}
            placeholder={token}
          />
        </div>

        <AlertDialogFooter>
          <AlertDialogCancel ref={cancelRef} disabled={reconcile.isPending}>
            {t("publications.reconcile.cancel")}
          </AlertDialogCancel>
          <AlertDialogAction
            disabled={!matched || reconcile.isPending}
            className="bg-destructive text-destructive-foreground hover:bg-destructive/90 focus-visible:ring-destructive/20"
            onClick={(e) => {
              e.preventDefault();
              void handleConfirm();
            }}
          >
            {reconcile.isPending ? t("common.loading") : t("publications.reconcile.confirm")}
          </AlertDialogAction>
        </AlertDialogFooter>
      </AlertDialogContent>
    </AlertDialog>
  );
}
