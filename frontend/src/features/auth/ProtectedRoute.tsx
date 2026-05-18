import type { ReactNode } from "react";
import { Navigate } from "react-router";
import { useMe } from "./useAuth";

export function ProtectedRoute({ children }: { children: ReactNode }) {
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

  return <>{children}</>;
}
