import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { api } from "@/lib/api";
import type {
  CreateInfobaseInput,
  InfobaseDetail,
  InfobaseListResponse,
  UpdateInfobaseInput,
} from "./types";

export const infobasesQueryKey = ["infobases"] as const;

export function useInfobases(tenantId?: string | null) {
  const qs = new URLSearchParams({ page: "1", pageSize: "200" });
  if (tenantId) {
    qs.set("tenantId", tenantId);
  }
  return useQuery({
    queryKey: [...infobasesQueryKey, { tenantId: tenantId ?? null }],
    queryFn: () => api<InfobaseListResponse>(`/api/v1/infobases?${qs.toString()}`),
  });
}

export function useCreateInfobase() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (input: CreateInfobaseInput) =>
      api<InfobaseDetail>("/api/v1/infobases", { method: "POST", body: input }),
    onSuccess: () => {
      void qc.invalidateQueries({ queryKey: infobasesQueryKey });
    },
  });
}

export function useUpdateInfobase() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: ({ id, input }: { id: string; input: UpdateInfobaseInput }) =>
      api<InfobaseDetail>(`/api/v1/infobases/${id}`, { method: "PUT", body: input }),
    onSuccess: () => {
      void qc.invalidateQueries({ queryKey: infobasesQueryKey });
    },
  });
}

export function useDeleteInfobase() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (id: string) =>
      api<null>(`/api/v1/infobases/${id}`, { method: "DELETE" }),
    onSuccess: () => {
      void qc.invalidateQueries({ queryKey: infobasesQueryKey });
    },
  });
}
