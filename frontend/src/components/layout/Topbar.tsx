import { useState } from "react";
import { KeyRoundIcon, LogOutIcon } from "lucide-react";
import { useTranslation } from "react-i18next";
import { useNavigate } from "react-router";
import { toast } from "sonner";
import { Button } from "@/components/ui/button";
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogHeader,
  DialogTitle,
} from "@/components/ui/dialog";
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuLabel,
  DropdownMenuSeparator,
  DropdownMenuTrigger,
} from "@/components/ui/dropdown-menu";
import { Separator } from "@/components/ui/separator";
import { SidebarTrigger } from "@/components/ui/sidebar";
import { useLogout, useMe } from "@/features/auth/useAuth";
import { ChangePasswordForm } from "@/features/profile/ChangePasswordForm";
import { ThemeToggle } from "./ThemeToggle";

const ENV_LABELS: Record<string, string> = {
  development: "dev",
  production: "prod",
  test: "test",
};

export function Topbar() {
  const { t } = useTranslation();
  const navigate = useNavigate();
  const { data: me } = useMe();
  const logout = useLogout();
  const [passwordDialogOpen, setPasswordDialogOpen] = useState(false);

  const envMode = import.meta.env.MODE;
  const envLabel = ENV_LABELS[envMode] ?? envMode;
  const userName = me?.userName ?? "";
  const initials = userName ? userName.slice(0, 2).toUpperCase() : "··";
  // Роль под логином в дропдауне (MLC-084: страницы /profile больше нет, это
  // единственное место, где пользователь видит свою роль). Метки — из словаря
  // раздела «Пользователи»; незнакомая роль показывается сырым именем.
  const roleLabel = (me?.roles ?? [])
    .map((role) => t(`users.roles.${role}`, { defaultValue: role }))
    .join(", ");

  const onLogout = async () => {
    await logout.mutateAsync();
    toast.success(t("auth.loggedOut"));
    navigate("/login", { replace: true });
  };

  return (
    <header className="border-border bg-background/95 supports-[backdrop-filter]:bg-background/80 sticky top-0 z-20 flex h-14 shrink-0 items-center gap-3 border-b px-4 backdrop-blur">
      <SidebarTrigger />
      <Separator orientation="vertical" className="h-6" />
      <div className="flex items-baseline gap-2">
        <span className="text-sm font-semibold tracking-tight">MitLicense Center</span>
        {envMode !== "production" ? (
          <span className="border-border bg-muted text-muted-foreground rounded-md border px-1.5 py-0.5 font-mono text-[10px] tracking-wide uppercase">
            {envLabel}
          </span>
        ) : null}
      </div>

      <div className="ml-auto flex items-center gap-2">
        <ThemeToggle />
        <DropdownMenu>
          <DropdownMenuTrigger asChild>
            <Button variant="ghost" size="sm" className="gap-2">
              <span className="bg-muted text-muted-foreground flex size-7 items-center justify-center rounded-full text-xs font-medium">
                {initials}
              </span>
              <span className="hidden text-sm font-medium sm:inline">{userName}</span>
            </Button>
          </DropdownMenuTrigger>
          <DropdownMenuContent align="end" className="w-56">
            <DropdownMenuLabel className="font-normal">
              <div className="flex flex-col gap-0.5">
                <span className="text-sm font-medium">{userName}</span>
                <span className="text-muted-foreground text-xs">{roleLabel || "—"}</span>
              </div>
            </DropdownMenuLabel>
            <DropdownMenuSeparator />
            <DropdownMenuItem onSelect={() => setPasswordDialogOpen(true)}>
              <KeyRoundIcon aria-hidden="true" />
              {t("profile.password.title")}
            </DropdownMenuItem>
            <DropdownMenuSeparator />
            <DropdownMenuItem onSelect={onLogout} disabled={logout.isPending}>
              <LogOutIcon aria-hidden="true" />
              {t("auth.signOut")}
            </DropdownMenuItem>
          </DropdownMenuContent>
        </DropdownMenu>
      </div>

      <Dialog open={passwordDialogOpen} onOpenChange={setPasswordDialogOpen}>
        <DialogContent className="sm:max-w-md">
          <DialogHeader>
            <DialogTitle>{t("profile.password.title")}</DialogTitle>
            <DialogDescription>{t("profile.password.subtitle")}</DialogDescription>
          </DialogHeader>
          <ChangePasswordForm showReset={false} onSuccess={() => setPasswordDialogOpen(false)} />
        </DialogContent>
      </Dialog>
    </header>
  );
}
