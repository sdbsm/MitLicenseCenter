import { useQuery } from "@tanstack/react-query";
import { api } from "@/lib/api";
import { hostMetricsSnapshotSchema } from "./types";

export const hostMetricsQueryKey = ["performance", "host"] as const;

// Live host-снимок раздела «Быстродействие» (MLC-064/065, ADR-26). Pull-по-требованию:
// бэкенд читает WMI/Process на каждый poll и ничего не персистит — сбор идёт, только
// пока вкладка открыта. 5с согласовано с паттерном `useSessionsSnapshot` (near-realtime),
// placeholderData оставляет прежний снимок на экране между poll'ами (без мигания).
// Схема — критичная граница (MLC-016): дельта-метрики питают вердикт «почему тормозит».
export function useHostMetrics() {
  return useQuery({
    queryKey: hostMetricsQueryKey,
    queryFn: () => api("/api/v1/performance/host", { schema: hostMetricsSnapshotSchema }),
    refetchInterval: 5_000,
    refetchOnWindowFocus: true,
    placeholderData: (prev) => prev,
  });
}
