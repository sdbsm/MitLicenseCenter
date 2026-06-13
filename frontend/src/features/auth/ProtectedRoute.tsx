import type { ReactNode } from "react";
import { useTranslation } from "react-i18next";
import { Navigate } from "react-router";
import { ApiNetworkError, ApiSchemaError } from "@/lib/api";
import { ForcePasswordChange } from "./ForcePasswordChange";
import { useMe } from "./useAuth";

interface ProtectedRouteProps {
  children: ReactNode;
  requireAdmin?: boolean;
}

// requireAdmin: вложенно оборачивает admin-only страницы. Перенаправляет
// на «/» (а не на /login), чтобы залогиненный Viewer не путался — у него уже
// есть доступ к остальным разделам.
export function ProtectedRoute({ children, requireAdmin = false }: ProtectedRouteProps) {
  const { t } = useTranslation();
  const { data, isLoading, isError, error, refetch } = useMe();

  if (isLoading) {
    return (
      <div className="flex h-full items-center justify-center">
        <div className="border-muted border-t-foreground h-10 w-10 animate-spin rounded-full border-2" />
      </div>
    );
  }

  // UX-03/FE-05 — НЕ выкидываем молча на /login при сетевом/схемном сбое: реальный
  // 401 (протухший сеанс) уже увёл на /login через onUnauthorized (fetchMe вернул
  // null без ошибки), поэтому `null` без ошибки → /login. А `isError` из-за нет-связи
  // или расхождения схемы — это не «разлогинен»: показываем экран «нет связи» с
  // «Повторить» (refetch), не сбрасывая сессию.
  if (isError && (error instanceof ApiNetworkError || error instanceof ApiSchemaError)) {
    const message = error instanceof ApiNetworkError ? t("errors.network") : t("errors.generic");
    return (
      <div className="flex h-full flex-col items-center justify-center gap-3 p-6 text-center">
        <p className="text-sm font-medium">{message}</p>
        <button
          type="button"
          className="text-primary text-sm underline"
          onClick={() => {
            void refetch();
          }}
        >
          {t("common.refresh")}
        </button>
      </div>
    );
  }

  if (isError || !data) {
    return <Navigate to="/login" replace />;
  }

  // MLC-059 — пока стоит флаг форс-смены (вход по временному паролю), не пускаем ни на
  // одну защищённую страницу: внешний ProtectedRoute оборачивает весь AppShell, поэтому
  // блокирующий экран замещает приложение целиком (сайдбар/контент не рендерятся).
  if (data.mustChangePassword) {
    return <ForcePasswordChange />;
  }

  if (requireAdmin && !data.roles.includes("Admin")) {
    return <Navigate to="/" replace />;
  }

  return <>{children}</>;
}
