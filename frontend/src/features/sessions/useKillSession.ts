import { useMutation, useQueryClient } from "@tanstack/react-query";
import { api } from "@/lib/api";
import { sessionsSnapshotQueryKey } from "./useSessionsSnapshot";

export function useKillSession() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: ({ id, reason }: { id: string; reason?: string }) =>
      api<null>(`/api/v1/sessions/${id}/kill`, { method: "POST", body: { reason } }),
    onSuccess: () => {
      void qc.invalidateQueries({ queryKey: sessionsSnapshotQueryKey });
    },
  });
}
