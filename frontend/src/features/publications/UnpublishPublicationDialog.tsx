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
import { useUnpublish } from "./usePublications";

interface ConflictBody {
  code?: string;
  detail?: string;
  title?: string;
}

interface UnpublishPublicationDialogProps {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  publication: PublicationListItem | null;
}

// Диалог снятия публикации из IIS через webinst -delete (MLC-113, UX-43). Подтверждение
// токеном (имя сайта + путь), деструктивная кнопка — снятие удаляет приложение IIS,
// default.vrd и web.config; инфобаза в кластере не затрагивается.
export function UnpublishPublicationDialog({
  open,
  onOpenChange,
  publication,
}: UnpublishPublicationDialogProps) {
  const { t } = useTranslation();
  const cancelRef = useRef<HTMLButtonElement>(null);
  const [confirmation, setConfirmation] = useState("");
  const unpublish = useUnpublish();

  if (!publication) {
    return null;
  }

  const token = `${publication.siteName}${publication.virtualPath}`;
  const matched = confirmation.trim() === token;

  const handleConfirm = async () => {
    try {
      await unpublish.mutateAsync(publication.id);
      toast.success(t("publications.toasts.unpublished"));
      onOpenChange(false);
    } catch (error) {
      if (error instanceof ApiError && error.status === 409) {
        const body = error.body as ConflictBody | null;
        const detail = body?.detail ?? body?.title ?? t("publications.toasts.unpublishFailed");
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
          <AlertDialogTitle>{t("publications.unpublish.title")}</AlertDialogTitle>
          <AlertDialogDescription>
            {t("publications.unpublish.body", {
              siteName: publication.siteName,
              virtualPath: publication.virtualPath,
            })}
          </AlertDialogDescription>
        </AlertDialogHeader>

        <div className="grid gap-2">
          <Label htmlFor="confirm-unpublish-token" className="text-sm">
            {t("publications.unpublish.confirmLabel", { token })}
          </Label>
          <Input
            id="confirm-unpublish-token"
            autoComplete="off"
            value={confirmation}
            onChange={(e) => setConfirmation(e.target.value)}
            placeholder={token}
          />
        </div>

        <AlertDialogFooter>
          <AlertDialogCancel ref={cancelRef} disabled={unpublish.isPending}>
            {t("publications.unpublish.cancel")}
          </AlertDialogCancel>
          <AlertDialogAction
            disabled={!matched || unpublish.isPending}
            className="bg-destructive text-destructive-foreground hover:bg-destructive/90 focus-visible:ring-destructive/20"
            onClick={(e) => {
              e.preventDefault();
              void handleConfirm();
            }}
          >
            {unpublish.isPending ? t("common.loading") : t("publications.unpublish.confirm")}
          </AlertDialogAction>
        </AlertDialogFooter>
      </AlertDialogContent>
    </AlertDialog>
  );
}
