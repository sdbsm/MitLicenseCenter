import { useQuery } from "@tanstack/react-query";
import { api } from "@/lib/api";
import type { SessionsSnapshotResponse } from "./types";

export const sessionsSnapshotQueryKey = ["sessions", "snapshot"] as const;

export function useSessionsSnapshot() {
  return useQuery({
    queryKey: sessionsSnapshotQueryKey,
    queryFn: () => api<SessionsSnapshotResponse>("/api/v1/sessions/snapshot"),
    // Stage 3: реальный adapter подключён, polling 15s (docs/05_UI_REQUIREMENTS.md §4).
    refetchInterval: 15_000,
  });
}
