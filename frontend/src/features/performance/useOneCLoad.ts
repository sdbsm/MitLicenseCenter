import { useQuery } from "@tanstack/react-query";
import { api } from "@/lib/api";
import { oneCLoadSnapshotSchema } from "./types";

export const oneCLoadQueryKey = ["performance", "onec-sessions"] as const;

// Live-снимок нагрузки 1С «кто грузит внутри 1С» (MLC-066/067, ADR-26). Pull-по-требованию:
// бэкенд на каждый poll спавнит `rac session list` + `rac process list` и ничего не персистит —
// сбор идёт, только пока вкладка открыта. 5с согласовано с host-снимком и `useSessionsSnapshot`
// (near-realtime). placeholderData оставляет прежний снимок между poll'ами (без мигания).
// Схема — критичная граница (MLC-016): perf-поля питают подсветку «кто грузит/заблокирован».
export function useOneCLoad(paused = false) {
  return useQuery({
    queryKey: oneCLoadQueryKey,
    queryFn: () => api("/api/v1/performance/onec-sessions", { schema: oneCLoadSnapshotSchema }),
    // MLC-156: при паузе страницы авто-обновление выключается (общий контрол LiveControls).
    refetchInterval: paused ? false : 5_000,
    refetchOnWindowFocus: true,
    placeholderData: (prev) => prev,
  });
}
