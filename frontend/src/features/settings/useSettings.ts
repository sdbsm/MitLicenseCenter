import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { api } from "@/lib/api";
import type { SettingDescriptor } from "./types";

export const settingsQueryKey = ["settings"] as const;

export function useSettings() {
  return useQuery({
    queryKey: settingsQueryKey,
    queryFn: () => api<SettingDescriptor[]>("/api/v1/settings"),
  });
}

export function useUpdateSetting() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: ({ key, value }: { key: string; value: string | null }) =>
      api<null>(`/api/v1/settings/${encodeURIComponent(key)}`, {
        method: "PUT",
        body: { value },
      }),
    onSuccess: () => {
      void qc.invalidateQueries({ queryKey: settingsQueryKey });
    },
  });
}
