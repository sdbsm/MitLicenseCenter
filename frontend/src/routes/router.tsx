import { Navigate, createBrowserRouter } from "react-router";
import { AppShell } from "@/components/layout/AppShell";
import { ComingSoonPage } from "@/components/layout/ComingSoonPage";
import { LoginPage } from "@/features/auth/LoginPage";
import { ProtectedRoute } from "@/features/auth/ProtectedRoute";
import { DashboardPage } from "@/features/dashboard/DashboardPage";
import { ProfilePage } from "@/features/profile/ProfilePage";
import { InfobasesPage } from "@/features/infobases/InfobasesPage";
import { TenantsPage } from "@/features/tenants/TenantsPage";

export const router = createBrowserRouter([
  { path: "/login", element: <LoginPage /> },
  {
    element: (
      <ProtectedRoute>
        <AppShell />
      </ProtectedRoute>
    ),
    children: [
      { index: true, element: <DashboardPage /> },
      { path: "profile", element: <ProfilePage /> },
      { path: "tenants", element: <TenantsPage /> },
      { path: "infobases", element: <InfobasesPage /> },
      { path: "publications", element: <ComingSoonPage titleKey="nav.publications" /> },
      { path: "sessions", element: <ComingSoonPage titleKey="nav.sessions" /> },
      { path: "audit", element: <ComingSoonPage titleKey="nav.audit" /> },
      { path: "*", element: <Navigate to="/" replace /> },
    ],
  },
]);
