import { useState } from "react";
import { useTranslation } from "react-i18next";
import { toast } from "sonner";
import { Button } from "@/components/ui/button";
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from "@/components/ui/dialog";
import { matchConflictCode } from "@/lib/apiErrors";
import { USER_ROLES, type User, type UserRole } from "./types";
import { useChangeUserRole } from "./useUsers";

interface ChangeRoleDialogProps {
  user: User | null;
  open: boolean;
  onOpenChange: (open: boolean) => void;
}

function currentRole(user: User): UserRole {
  return (USER_ROLES.find((r) => user.roles.includes(r)) ?? "Viewer") as UserRole;
}

export function ChangeRoleDialog({ user, open, onOpenChange }: ChangeRoleDialogProps) {
  const { t } = useTranslation();
  const change = useChangeUserRole();
  const [role, setRole] = useState<UserRole>(user ? currentRole(user) : "Viewer");

  if (!user) {
    return null;
  }

  const handleConfirm = async () => {
    try {
      await change.mutateAsync({ id: user.id, role });
      toast.success(t("users.toasts.roleChanged", { name: user.userName }));
      onOpenChange(false);
    } catch (error) {
      // Серверные guard'ы → понятный тост; диалог остаётся открытым.
      const messageKey = matchConflictCode(error, {
        USER_CANNOT_CHANGE_OWN_ROLE: "users.errors.cannotChangeOwnRole",
        USER_LAST_ACTIVE: "users.errors.lastActiveAdminDemote",
      });
      toast.error(messageKey ? t(messageKey) : t("errors.generic"));
    }
  };

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="sm:max-w-lg">
        <DialogHeader>
          <DialogTitle>{t("users.changeRole.title")}</DialogTitle>
          <DialogDescription>
            {t("users.changeRole.subtitle", { name: user.userName })}
          </DialogDescription>
        </DialogHeader>

        <div role="radiogroup" className="grid gap-2">
          {USER_ROLES.map((r: UserRole) => (
            <label
              key={r}
              className="hover:bg-accent/50 flex cursor-pointer items-start gap-3 rounded-md border px-3 py-2"
            >
              <input
                type="radio"
                name="role"
                value={r}
                checked={role === r}
                onChange={() => setRole(r)}
                className="mt-0.5 size-4 cursor-pointer"
              />
              <span className="grid gap-0.5">
                <span className="text-sm font-medium">{t(`users.roles.${r}`)}</span>
                <span className="text-muted-foreground text-xs">{t(`users.roleHints.${r}`)}</span>
              </span>
            </label>
          ))}
        </div>

        <p className="text-muted-foreground text-xs">{t("users.changeRole.hint")}</p>

        <DialogFooter className="gap-2">
          <Button
            type="button"
            variant="ghost"
            disabled={change.isPending}
            onClick={() => onOpenChange(false)}
          >
            {t("common.cancel")}
          </Button>
          <Button
            type="button"
            disabled={change.isPending}
            onClick={() => {
              void handleConfirm();
            }}
          >
            {change.isPending ? t("common.loading") : t("users.changeRole.confirm")}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}
