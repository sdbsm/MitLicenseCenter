import { LogOutIcon, UserIcon } from "lucide-react";
import { useTranslation } from "react-i18next";
import { useNavigate } from "react-router";
import { toast } from "sonner";
import { Button } from "@/components/ui/button";
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

  const envMode = import.meta.env.MODE;
  const envLabel = ENV_LABELS[envMode] ?? envMode;
  const userName = me?.userName ?? "";
  const initials = userName ? userName.slice(0, 2).toUpperCase() : "··";

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
                <span className="text-muted-foreground text-xs">{t("auth.signedIn")}</span>
              </div>
            </DropdownMenuLabel>
            <DropdownMenuSeparator />
            <DropdownMenuItem onSelect={() => navigate("/profile")}>
              <UserIcon aria-hidden="true" />
              {t("nav.profile")}
            </DropdownMenuItem>
            <DropdownMenuSeparator />
            <DropdownMenuItem onSelect={onLogout} disabled={logout.isPending}>
              <LogOutIcon aria-hidden="true" />
              {t("auth.signOut")}
            </DropdownMenuItem>
          </DropdownMenuContent>
        </DropdownMenu>
      </div>
    </header>
  );
}
