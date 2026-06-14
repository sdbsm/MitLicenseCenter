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
import { usePublish } from "./usePublications";

interface ConflictBody {
  code?: string;
  detail?: string;
  title?: string;
}

interface PublishPublicationDialogProps {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  publication: PublicationListItem | null;
}

// Диалог публикации через webinst (MLC-045). Простое «да/нет» без ручного ввода токена
// (ADR-45: публикация — обратимое действие; результат восстановим переопубликацией).
// Если публикация сделана не панелью (source ≠ Webinst) — показываем предупреждение о
// перезаписи ручной конфигурации; confirm=true отправляется всегда (явное действие).
export function PublishPublicationDialog({
  open,
  onOpenChange,
  publication,
}: PublishPublicationDialogProps) {
  const { t } = useTranslation();
  const publish = usePublish();

  if (!publication) {
    return null;
  }

  const isOverwrite = publication.source !== "Webinst";

  const handleConfirm = async () => {
    try {
      await publish.mutateAsync({ id: publication.id, confirm: true });
      toast.success(t("publications.toasts.published"));
      onOpenChange(false);
    } catch (error) {
      if (error instanceof ApiError && error.status === 409) {
        const body = error.body as ConflictBody | null;
        const detail = body?.detail ?? body?.title ?? t("publications.toasts.publishFailed");
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
          <AlertDialogTitle>{t("publications.publish.title")}</AlertDialogTitle>
          <AlertDialogDescription>
            {isOverwrite
              ? t("publications.publish.bodyOverwrite", {
                  siteName: publication.siteName,
                  virtualPath: publication.virtualPath,
                })
              : t("publications.publish.body", {
                  siteName: publication.siteName,
                  virtualPath: publication.virtualPath,
                })}
          </AlertDialogDescription>
        </AlertDialogHeader>

        <AlertDialogFooter>
          <AlertDialogCancel disabled={publish.isPending}>
            {t("publications.publish.cancel")}
          </AlertDialogCancel>
          <AlertDialogAction
            disabled={publish.isPending}
            onClick={(e) => {
              e.preventDefault();
              void handleConfirm();
            }}
          >
            {publish.isPending ? t("common.loading") : t("publications.publish.confirm")}
          </AlertDialogAction>
        </AlertDialogFooter>
      </AlertDialogContent>
    </AlertDialog>
  );
}
