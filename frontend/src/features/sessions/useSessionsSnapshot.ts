import { useQuery } from "@tanstack/react-query";
import { api } from "@/lib/api";
import { sessionsSnapshotResponseSchema } from "./types";

export const sessionsSnapshotQueryKey = ["sessions", "snapshot"] as const;

export function useSessionsSnapshot() {
  return useQuery({
    queryKey: sessionsSnapshotQueryKey,
    queryFn: () => api("/api/v1/sessions/snapshot", { schema: sessionsSnapshotResponseSchema }),
    refetchInterval: 15_000,
    refetchOnWindowFocus: true,
    placeholderData: (prev) => prev,
  });
}
