import { useQuery } from "@tanstack/react-query";
import { api } from "@/lib/api";
import { dashboardSummarySchema } from "./types";

export const dashboardSummaryQueryKey = ["dashboard", "summary"] as const;

export function useDashboardSummary() {
  return useQuery({
    queryKey: dashboardSummaryQueryKey,
    // FE-19 (MLC-120): ответ проходит толерантную Zod-границу (как backups), а не
    // слепой каст. Поведение для всех валидных ответов бэкенда сохраняется.
    queryFn: () => api("/api/v1/dashboard/summary", { schema: dashboardSummarySchema }),
    // MLC-044: 5с согласовано с hot-каденцией (~4с). Сводка дешёвая (несколько COUNT +
    // in-memory RAS-health); рост вызовов 15→5с пренебрежим при 5–20 пользователях.
    refetchInterval: 5_000,
    refetchOnWindowFocus: true,
    placeholderData: (prev) => prev,
  });
}
