import type { ReactNode } from "react";
import { Navigate } from "react-router";
import { useMe } from "./useAuth";

interface ProtectedRouteProps {
  children: ReactNode;
  requireAdmin?: boolean;
}

// requireAdmin: вложенно оборачивает admin-only страницы. Перенаправляет
// на «/» (а не на /login), чтобы залогиненный Viewer не путался — у него уже
// есть доступ к остальным разделам.
export function ProtectedRoute({ children, requireAdmin = false }: ProtectedRouteProps) {
  const { data, isLoading, isError } = useMe();

  if (isLoading) {
    return (
      <div className="flex h-full items-center justify-center">
        <div className="border-muted border-t-foreground h-10 w-10 animate-spin rounded-full border-2" />
      </div>
    );
  }

  if (isError || !data) {
    return <Navigate to="/login" replace />;
  }

  if (requireAdmin && !data.roles.includes("Admin")) {
    return <Navigate to="/" replace />;
  }

  return <>{children}</>;
}
