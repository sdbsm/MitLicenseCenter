import { useQueryClient } from "@tanstack/react-query";
import { useTranslation } from "react-i18next";
import { useNavigate } from "react-router";
import { toast } from "sonner";
import { Button } from "@/components/ui/button";
import { ChangePasswordForm } from "@/features/profile/ChangePasswordForm";
import { ME_KEY, useLogout } from "./useAuth";

/**
 * Блокирующий экран форс-смены пароля (MLC-059). Показывается `ProtectedRoute`, пока у
 * текущего пользователя стоит флаг `mustChangePassword` (вошёл по временному паролю после
 * создания/сброса учётки). На остальные страницы не пускает; разрешён только выход.
 * После успешной смены инвалидируем `/me` — флаг снимается, гейт пропадает.
 */
export function ForcePasswordChange() {
  const { t } = useTranslation();
  const qc = useQueryClient();
  const navigate = useNavigate();
  const logout = useLogout();

  const onLogout = async () => {
    await logout.mutateAsync();
    toast.success(t("auth.loggedOut"));
    navigate("/login", { replace: true });
  };

  return (
    <div className="bg-background flex min-h-svh items-center justify-center px-4">
      <div className="border-border bg-card w-full max-w-md rounded-xl border p-8 shadow-sm">
        <div className="mb-6 space-y-1">
          <h1 className="text-2xl font-semibold tracking-tight">{t("auth.forceChange.title")}</h1>
          <p className="text-muted-foreground text-sm">{t("auth.forceChange.subtitle")}</p>
        </div>

        <ChangePasswordForm
          showReset={false}
          submitLabel={t("auth.forceChange.submit")}
          onSuccess={() => {
            void qc.invalidateQueries({ queryKey: ME_KEY });
          }}
        />

        <div className="mt-4 border-t pt-4 text-center">
          <Button variant="link" className="text-muted-foreground" onClick={onLogout}>
            {t("auth.signOut")}
          </Button>
        </div>
      </div>
    </div>
  );
}
