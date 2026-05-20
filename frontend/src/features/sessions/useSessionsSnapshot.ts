import { useQuery } from "@tanstack/react-query";
import { api } from "@/lib/api";
import type { SessionsSnapshotResponse } from "./types";

export const sessionsSnapshotQueryKey = ["sessions", "snapshot"] as const;

export function useSessionsSnapshot() {
  return useQuery({
    queryKey: sessionsSnapshotQueryKey,
    queryFn: () => api<SessionsSnapshotResponse>("/api/v1/sessions/snapshot"),
    refetchInterval: 15_000,
    refetchOnWindowFocus: true,
    placeholderData: (prev) => prev,
  });
}
