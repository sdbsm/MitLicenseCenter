import { useQuery } from "@tanstack/react-query";
import { api } from "@/lib/api";
import type { DashboardSummaryResponse } from "./types";

export const dashboardSummaryQueryKey = ["dashboard", "summary"] as const;

export function useDashboardSummary() {
  return useQuery({
    queryKey: dashboardSummaryQueryKey,
    queryFn: () => api<DashboardSummaryResponse>("/api/v1/dashboard/summary"),
    refetchInterval: 15_000,
    refetchOnWindowFocus: true,
    placeholderData: (prev) => prev,
  });
}
