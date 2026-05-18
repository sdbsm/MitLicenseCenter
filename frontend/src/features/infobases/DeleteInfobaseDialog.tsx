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
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import type { InfobaseListItem } from "./types";
import { useDeleteInfobase } from "./useInfobases";

interface DeleteInfobaseDialogProps {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  infobase: InfobaseListItem | null;
}

export function DeleteInfobaseDialog({ open, onOpenChange, infobase }: DeleteInfobaseDialogProps) {
  const { t } = useTranslation();
  const [confirmation, setConfirmation] = useState("");
  const remove = useDeleteInfobase();

  if (!infobase) {
    return null;
  }

  const matched = confirmation.trim() === infobase.name;

  const handleConfirm = async () => {
    try {
      await remove.mutateAsync(infobase.id);
      toast.success(t("infobases.toasts.deleted", { name: infobase.name }));
      onOpenChange(false);
    } catch {
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

        <div className="grid gap-2">
          <Label htmlFor="confirm-infobase-name" className="text-sm">
            {t("infobases.delete.confirmLabel", { name: infobase.name })}
          </Label>
          <Input
            id="confirm-infobase-name"
            autoComplete="off"
            value={confirmation}
            onChange={(e) => setConfirmation(e.target.value)}
            placeholder={infobase.name}
          />
        </div>

        <AlertDialogFooter>
          <AlertDialogCancel disabled={remove.isPending}>
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
            {remove.isPending ? t("common.loading") : t("infobases.delete.confirm")}
          </AlertDialogAction>
        </AlertDialogFooter>
      </AlertDialogContent>
    </AlertDialog>
  );
}
