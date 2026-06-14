import { useQuery } from "@tanstack/react-query";
import { api } from "@/lib/api";
import { sessionsSnapshotResponseSchema } from "./types";

export const sessionsSnapshotQueryKey = ["sessions", "snapshot"] as const;

export function useSessionsSnapshot(paused = false) {
  return useQuery({
    queryKey: sessionsSnapshotQueryKey,
    queryFn: () => api("/api/v1/sessions/snapshot", { schema: sessionsSnapshotResponseSchema }),
    // MLC-044: 5с согласовано с hot-каденцией (~4с) — экран отражает near-realtime
    // hot-enforce в пределах одного цикла. Снимок дешёвый (in-memory, 0 EF, 0 спавнов rac.exe).
    // MLC-156: при паузе авто-обновление выключается (оператор сам жмёт «Обновить сейчас»).
    refetchInterval: paused ? false : 5_000,
    refetchOnWindowFocus: true,
    placeholderData: (prev) => prev,
  });
}
