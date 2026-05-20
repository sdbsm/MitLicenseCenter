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
import type { SessionSnapshotEntry } from "./types";
import { useKillSession } from "./useKillSession";

interface KillSessionDialogProps {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  session: SessionSnapshotEntry | null;
}

export function KillSessionDialog({ open, onOpenChange, session }: KillSessionDialogProps) {
  const { t } = useTranslation();
  const cancelRef = useRef<HTMLButtonElement>(null);
  const [confirmation, setConfirmation] = useState("");
  const [reason, setReason] = useState("");
  const kill = useKillSession();

  if (!session) {
    return null;
  }

  const matched = confirmation.trim() === session.appId;

  const handleConfirm = async () => {
    try {
      await kill.mutateAsync({ id: session.sessionId, reason: reason.trim() || undefined });
      toast.success(t("sessions.kill.toastSuccess"));
      onOpenChange(false);
    } catch (error) {
      if (error instanceof ApiError && error.status === 404) {
        toast.error(t("sessions.kill.toastAlreadyGone"));
        onOpenChange(false);
        return;
      }
      toast.error(t("errors.generic"));
    }
  };

  return (
    <AlertDialog open={open} onOpenChange={onOpenChange}>
      <AlertDialogContent>
        <AlertDialogHeader>
          <AlertDialogTitle>{t("sessions.kill.title")}</AlertDialogTitle>
          <AlertDialogDescription>
            {t("sessions.kill.body", { appId: session.appId })}
          </AlertDialogDescription>
        </AlertDialogHeader>

        <div className="grid gap-4">
          <div className="grid gap-2">
            <Label htmlFor="confirm-app-id" className="text-sm">
              {t("sessions.kill.confirmLabel", { appId: session.appId })}
            </Label>
            <Input
              id="confirm-app-id"
              autoComplete="off"
              value={confirmation}
              onChange={(e) => setConfirmation(e.target.value)}
              placeholder={session.appId}
            />
          </div>

          <div className="grid gap-2">
            <Label htmlFor="kill-reason" className="text-muted-foreground text-sm">
              {t("sessions.kill.reasonLabel")}
            </Label>
            <Input
              id="kill-reason"
              autoComplete="off"
              value={reason}
              onChange={(e) => setReason(e.target.value)}
              placeholder={t("sessions.kill.reasonPlaceholder")}
            />
          </div>
        </div>

        <AlertDialogFooter>
          <AlertDialogCancel ref={cancelRef} disabled={kill.isPending}>
            {t("sessions.kill.cancel")}
          </AlertDialogCancel>
          <AlertDialogAction
            disabled={!matched || kill.isPending}
            className="bg-destructive text-destructive-foreground hover:bg-destructive/90 focus-visible:ring-destructive/20"
            onClick={(e) => {
              e.preventDefault();
              void handleConfirm();
            }}
          >
            {kill.isPending ? t("common.loading") : t("sessions.kill.confirm")}
          </AlertDialogAction>
        </AlertDialogFooter>
      </AlertDialogContent>
    </AlertDialog>
  );
}
