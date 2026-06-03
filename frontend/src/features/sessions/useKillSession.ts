import { api } from "@/lib/api";
import { useInvalidatingMutation } from "@/lib/useInvalidatingMutation";
import { sessionsSnapshotQueryKey } from "./useSessionsSnapshot";

export function useKillSession() {
  return useInvalidatingMutation({
    mutationFn: ({ id, reason }: { id: string; reason?: string }) =>
      api<null>(`/api/v1/sessions/${id}/kill`, { method: "POST", body: { reason } }),
    invalidate: sessionsSnapshotQueryKey,
  });
}
