import { useQuery } from "@tanstack/react-query";
import { api } from "@/lib/api";
import type { DashboardSummaryResponse } from "./types";

export const dashboardSummaryQueryKey = ["dashboard", "summary"] as const;

export function useDashboardSummary() {
  return useQuery({
    queryKey: dashboardSummaryQueryKey,
    queryFn: () => api<DashboardSummaryResponse>("/api/v1/dashboard/summary"),
    // MLC-044: 5с согласовано с hot-каденцией (~4с). Сводка дешёвая (несколько COUNT +
    // in-memory RAS-health); рост вызовов 15→5с пренебрежим при 5–20 пользователях.
    refetchInterval: 5_000,
    refetchOnWindowFocus: true,
    placeholderData: (prev) => prev,
  });
}
