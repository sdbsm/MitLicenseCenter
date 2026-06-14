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

// Диалог снятия публикации из IIS через webinst -delete (MLC-113, UX-43). Простое «да/нет»
// без ручного ввода токена (ADR-45: снятие публикации — обратимое действие; результат
// восстановим переопубликацией). Снятие удаляет приложение IIS, default.vrd и web.config;
// инфобаза в кластере не затрагивается.
export function UnpublishPublicationDialog({
  open,
  onOpenChange,
  publication,
}: UnpublishPublicationDialogProps) {
  const { t } = useTranslation();
  const unpublish = useUnpublish();

  if (!publication) {
    return null;
  }

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

        <AlertDialogFooter>
          <AlertDialogCancel disabled={unpublish.isPending}>
            {t("publications.unpublish.cancel")}
          </AlertDialogCancel>
          <AlertDialogAction
            disabled={unpublish.isPending}
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
