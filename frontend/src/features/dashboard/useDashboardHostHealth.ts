import { useQuery } from "@tanstack/react-query";
import { api } from "@/lib/api";
import { hostMetricsQueryKey } from "@/features/performance/useHostMetrics";
import { hostMetricsSnapshotSchema } from "@/features/performance/types";

// Каденция опроса здоровья хоста на дашборде (MLC-085, решение пользователя):
// 30–60 с, НЕ 5-секундная live-каденция /performance. Дашборд отвечает «есть ли
// проблема» — за ответом «какая и из-за кого» оператор уходит на /performance.
export const DASHBOARD_HOST_REFETCH_MS = 45_000;

// Строка здоровья хоста на дашборде (MLC-085): тот же GET /performance/host и та же
// схема-граница (MLC-016), что у раздела «Быстродействие», но редкий poll. queryKey
// общий с useHostMetrics — страницы не смонтированы одновременно, а при переходе
// дашборд ↔ /performance кэшированный снимок показывается сразу, без скелетона.
export function useDashboardHostHealth() {
  return useQuery({
    queryKey: hostMetricsQueryKey,
    queryFn: () => api("/api/v1/performance/host", { schema: hostMetricsSnapshotSchema }),
    refetchInterval: DASHBOARD_HOST_REFETCH_MS,
    refetchOnWindowFocus: true,
    placeholderData: (prev) => prev,
  });
}
