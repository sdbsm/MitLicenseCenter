import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { api } from "@/lib/api";
import { updateStatusSchema, type UpdateStatus } from "./types";

export const updatesStatusQueryKey = ["updates", "status"] as const;

// MLC-176 — статус обновлений для глобального баннера (виден всем ролям). Запрос
// проходит толерантную Zod-границу (по образцу useHealth). Результат на бэкенде
// кэшируется на Updates.CheckIntervalHours, поэтому держим его «свежим» 1 час и не
// рефетчим на фокусе. retry: 1 — одна попытка; при ошибке data остаётся undefined,
// баннер скрыт (не мешает работе, если GitHub недоступен).
export function useUpdateStatus() {
  return useQuery({
    queryKey: updatesStatusQueryKey,
    queryFn: () => api("/api/v1/updates/status", { schema: updateStatusSchema }),
    staleTime: 60 * 60_000,
    refetchOnWindowFocus: false,
    retry: 1,
  });
}

// Admin форсит немедленную проверку (карточка в «Параметрах»). Свежий статус кладём
// прямо в кэш — баннер и карточка обновляются без повторного запроса /status.
export function useCheckNow() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: () =>
      api<UpdateStatus>("/api/v1/updates/check-now", {
        method: "POST",
        schema: updateStatusSchema,
      }),
    onSuccess: (data) => {
      queryClient.setQueryData(updatesStatusQueryKey, data);
    },
  });
}
