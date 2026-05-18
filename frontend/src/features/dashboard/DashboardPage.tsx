import { useTranslation } from "react-i18next";
import { useNavigate } from "react-router";
import { toast } from "sonner";
import { useLogout, useMe } from "@/features/auth/useAuth";

export function DashboardPage() {
  const { t } = useTranslation();
  const { data: me } = useMe();
  const logout = useLogout();
  const navigate = useNavigate();

  const onLogout = async () => {
    await logout.mutateAsync();
    toast.success(t("auth.loggedOut"));
    navigate("/login", { replace: true });
  };

  return (
    <div className="flex min-h-svh flex-col">
      <header className="flex items-center justify-between border-b border-border bg-card px-6 py-3">
        <div className="flex items-baseline gap-3">
          <h1 className="text-lg font-semibold tracking-tight">MitLicense Center</h1>
          <span className="text-xs text-muted-foreground">Stage 1 · scaffold</span>
        </div>
        <div className="flex items-center gap-4">
          <span className="text-sm text-muted-foreground">{me?.userName}</span>
          <button
            type="button"
            onClick={onLogout}
            disabled={logout.isPending}
            className="rounded-md border border-border px-3 py-1.5 text-sm transition hover:bg-accent disabled:opacity-50"
          >
            {t("auth.signOut")}
          </button>
        </div>
      </header>

      <main className="flex-1 p-6">
        <div className="mb-6 space-y-1">
          <h2 className="text-2xl font-semibold tracking-tight">{t("dashboard.title")}</h2>
          <p className="text-sm text-muted-foreground">{t("dashboard.subtitle")}</p>
        </div>

        <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-4">
          {(["tenants", "sessions", "licenses", "cluster"] as const).map((key) => (
            <div
              key={key}
              className="rounded-xl border border-border bg-card p-5 shadow-sm"
            >
              <div className="text-xs uppercase tracking-wide text-muted-foreground">
                {key}
              </div>
              <div className="mt-2 text-3xl font-semibold tabular-nums">—</div>
              <div className="mt-1 text-xs text-muted-foreground">{t("common.noData")}</div>
            </div>
          ))}
        </div>

        <div className="mt-6 rounded-xl border border-dashed border-border p-6 text-sm text-muted-foreground">
          {t("dashboard.stub")}
        </div>
      </main>
    </div>
  );
}
