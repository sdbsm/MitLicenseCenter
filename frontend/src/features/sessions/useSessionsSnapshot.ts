import { useQuery } from "@tanstack/react-query";
import { api } from "@/lib/api";
import type { SessionsSnapshotResponse } from "./types";

export const sessionsSnapshotQueryKey = ["sessions", "snapshot"] as const;

export function useSessionsSnapshot() {
  return useQuery({
    queryKey: sessionsSnapshotQueryKey,
    queryFn: () => api<SessionsSnapshotResponse>("/api/v1/sessions/snapshot"),
    // Stage 2: endpoint всегда возвращает пустоту, поэтому реальный polling
    // (~15s, см. docs/05_UI_REQUIREMENTS.md) включаем в Stage 3.
    refetchInterval: false,
  });
}
