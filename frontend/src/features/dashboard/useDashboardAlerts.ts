import { useQuery } from "@tanstack/react-query";
import { api } from "@/lib/api";
import { dashboardAlertsSchema } from "./types";

export const dashboardAlertsQueryKey = ["dashboard", "alerts"] as const;

// MLC-186b — сигналы виджета «Требует внимания». Питается серверным агрегатом
// /dashboard/alerts (MLC-186a), проходящим толерантную Zod-границу.
//
// Каденция 30с (не 5с как summary): это сигналы, а не живые KPI — бэкенд кеширует
// агрегат на 30с, поэтому чаще опрашивать бессмысленно.
export function useDashboardAlerts() {
  return useQuery({
    queryKey: dashboardAlertsQueryKey,
    queryFn: () => api("/api/v1/dashboard/alerts", { schema: dashboardAlertsSchema }),
    refetchInterval: 30_000,
    refetchOnWindowFocus: true,
    placeholderData: (prev) => prev,
  });
}
