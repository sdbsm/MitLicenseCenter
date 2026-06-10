/* Это модуль конфигурации маршрутов, а не HMR-граница компонента: lazy()-консты
   страниц правило react-refresh принимает за определения компонентов рядом с
   не-компонентным экспортом `router`. Fast-refresh здесь неприменим — отключаем
   предупреждение для файла. */
/* eslint-disable react-refresh/only-export-components */
import { Suspense, lazy } from "react";
import { Navigate, createBrowserRouter } from "react-router";
import { AppShell } from "@/components/layout/AppShell";
import { PageFallback } from "@/components/PageFallback";
import { ProtectedRoute } from "@/features/auth/ProtectedRoute";

// Страницы маршрутов грузятся лениво (React.lazy), чтобы разбить единый бандл на
// чанки по страницам (MLC-018). Компоненты — именованные экспорты, поэтому
// маппим их в { default } для React.lazy. Каркас (AppShell/ProtectedRoute) и сам
// роутер остаются в главном чанке — они нужны на каждом маршруте.
const LoginPage = lazy(() =>
  import("@/features/auth/LoginPage").then((m) => ({ default: m.LoginPage }))
);
const DashboardPage = lazy(() =>
  import("@/features/dashboard/DashboardPage").then((m) => ({ default: m.DashboardPage }))
);
const ProfilePage = lazy(() =>
  import("@/features/profile/ProfilePage").then((m) => ({ default: m.ProfilePage }))
);
const TenantsPage = lazy(() =>
  import("@/features/tenants/TenantsPage").then((m) => ({ default: m.TenantsPage }))
);
const TenantDetailPage = lazy(() =>
  import("@/features/tenants/TenantDetailPage").then((m) => ({ default: m.TenantDetailPage }))
);
const InfobasesPage = lazy(() =>
  import("@/features/infobases/InfobasesPage").then((m) => ({ default: m.InfobasesPage }))
);
const SessionsPage = lazy(() =>
  import("@/features/sessions/SessionsPage").then((m) => ({ default: m.SessionsPage }))
);
const AuditPage = lazy(() =>
  import("@/features/audit/AuditPage").then((m) => ({ default: m.AuditPage }))
);
const ReportsPage = lazy(() =>
  import("@/features/reports/ReportsPage").then((m) => ({ default: m.ReportsPage }))
);
const PerformancePage = lazy(() =>
  import("@/features/performance/PerformancePage").then((m) => ({ default: m.PerformancePage }))
);
const SettingsPage = lazy(() =>
  import("@/features/settings/SettingsPage").then((m) => ({ default: m.SettingsPage }))
);
const UsersPage = lazy(() =>
  import("@/features/users/UsersPage").then((m) => ({ default: m.UsersPage }))
);

export const router = createBrowserRouter([
  {
    path: "/login",
    element: (
      <Suspense fallback={<PageFallback />}>
        <LoginPage />
      </Suspense>
    ),
  },
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
      { path: "tenants/:id", element: <TenantDetailPage /> },
      { path: "infobases", element: <InfobasesPage /> },
      { path: "sessions", element: <SessionsPage /> },
      { path: "reports", element: <ReportsPage /> },
      { path: "performance", element: <PerformancePage /> },
      { path: "audit", element: <AuditPage /> },
      {
        path: "settings",
        element: (
          <ProtectedRoute requireAdmin>
            <SettingsPage />
          </ProtectedRoute>
        ),
      },
      {
        path: "users",
        element: (
          <ProtectedRoute requireAdmin>
            <UsersPage />
          </ProtectedRoute>
        ),
      },
      { path: "*", element: <Navigate to="/" replace /> },
    ],
  },
]);
