import { useState } from "react";
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
import { Checkbox } from "@/components/ui/checkbox";
import { Label } from "@/components/ui/label";
import { ApiError } from "@/lib/api";
import type { PublicationPublishStatus } from "@/features/publications/types";
import { useDeleteInfobase } from "./useInfobases";

// Диалог удаления читает только id (мутация) и name (отображение), поэтому
// принимает минимальный контракт — это позволяет переиспользовать его и из диалога
// обратного дрейфа (MLC-096), где полного InfobaseListItem нет (запись может быть на
// другой странице пагинации). publishStatus (MLC-113) опционален: когда он определён,
// показываем чекбокс «снять публикацию из IIS» (по умолчанию отмечен при Published);
// в диалоге обратного дрейфа статус неизвестен — чекбокс скрыт (снимать нечего).
// ADR-45: необратимое действие — «да/нет» с явным предупреждением; ручной ввод убран.
export interface DeletableInfobase {
  id: string;
  name: string;
  publishStatus?: PublicationPublishStatus;
}

interface DeleteInfobaseDialogProps {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  infobase: DeletableInfobase | null;
}

export function DeleteInfobaseDialog({ open, onOpenChange, infobase }: DeleteInfobaseDialogProps) {
  const { t } = useTranslation();
  // Чекбокс показываем только когда статус публикации известен (страница «Базы»).
  // По умолчанию отмечен, если публикация сейчас на месте (Published) — типичный сценарий
  // «удаляю базу целиком, чищу и IIS». При других статусах снимать обычно нечего → unchecked.
  const showUnpublishOption = infobase?.publishStatus !== undefined;
  const [unpublishFromIis, setUnpublishFromIis] = useState(infobase?.publishStatus === "Published");
  const remove = useDeleteInfobase();

  if (!infobase) {
    return null;
  }

  const handleConfirm = async () => {
    try {
      await remove.mutateAsync({
        id: infobase.id,
        unpublishFromIis: showUnpublishOption ? unpublishFromIis : undefined,
      });
      toast.success(t("infobases.toasts.deleted", { name: infobase.name }));
      onOpenChange(false);
    } catch (error) {
      // 409 при снятии из IIS — БД не тронута; покажем причину (detail из бэка).
      if (error instanceof ApiError && error.status === 409) {
        const body = error.body as { detail?: string; title?: string } | null;
        toast.error(body?.detail ?? body?.title ?? t("publications.toasts.unpublishFailed"));
        return;
      }
      toast.error(t("errors.generic"));
    }
  };

  return (
    <AlertDialog open={open} onOpenChange={onOpenChange}>
      <AlertDialogContent>
        <AlertDialogHeader>
          <AlertDialogTitle>{t("infobases.delete.title")}</AlertDialogTitle>
          <AlertDialogDescription>
            {t("infobases.delete.body", { name: infobase.name })}
          </AlertDialogDescription>
        </AlertDialogHeader>

        <p className="text-destructive text-sm font-medium">
          {t("infobases.delete.irreversibleWarning")}
        </p>

        {showUnpublishOption && (
          <div className="flex items-start gap-2">
            <Checkbox
              id="delete-unpublish-from-iis"
              checked={unpublishFromIis}
              onCheckedChange={(v) => setUnpublishFromIis(v === true)}
              disabled={remove.isPending}
            />
            <Label
              htmlFor="delete-unpublish-from-iis"
              className="text-muted-foreground text-sm leading-snug font-normal"
            >
              {t("infobases.delete.unpublishOption")}
            </Label>
          </div>
        )}

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
            {remove.isPending ? t("common.loading") : t("infobases.delete.confirm")}
          </AlertDialogAction>
        </AlertDialogFooter>
      </AlertDialogContent>
    </AlertDialog>
  );
}
