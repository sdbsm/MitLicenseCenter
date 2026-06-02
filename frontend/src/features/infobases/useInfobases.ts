import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { api } from "@/lib/api";
import { tenantsQueryKey } from "@/features/tenants/useTenants";
import type {
  ClusterIdAvailability,
  CreateInfobaseInput,
  InfobaseDetail,
  InfobaseListResponse,
  UpdateInfobaseInput,
} from "./types";

export const infobasesQueryKey = ["infobases"] as const;

export const INFOBASES_PAGE_SIZE = 25;

const GUID_PATTERN =
  /^[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}$/;

// Серверная пагинация (MLC-015). queryKey включает фильтр и page/pageSize; префикс
// остаётся `["infobases"]`, поэтому мутации инвалидируют все страницы/фильтры разом.
export function useInfobases(tenantId?: string | null, page = 1, pageSize = INFOBASES_PAGE_SIZE) {
  const qs = new URLSearchParams({ page: String(page), pageSize: String(pageSize) });
  if (tenantId) {
    qs.set("tenantId", tenantId);
  }
  return useQuery({
    queryKey: [...infobasesQueryKey, { tenantId: tenantId ?? null, page, pageSize }],
    queryFn: () => api<InfobaseListResponse>(`/api/v1/infobases?${qs.toString()}`),
    // Не моргаем скелетоном при смене страницы/фильтра — показываем предыдущие данные.
    placeholderData: (prev) => prev,
  });
}

// Точечная проверка занятости базы кластера (MLC-015) — вместо выгрузки всех баз во
// фронтовой форме. Дёргается при выборе/вводе валидного GUID базы; `excludeId` исключает
// собственную базу в режиме редактирования.
export function useClusterIdAvailability(
  clusterInfobaseId: string,
  excludeId?: string,
  enabled = true
) {
  const isGuid = GUID_PATTERN.test(clusterInfobaseId);
  const qs = new URLSearchParams({ clusterInfobaseId });
  if (excludeId) {
    qs.set("excludeId", excludeId);
  }
  return useQuery({
    queryKey: [
      ...infobasesQueryKey,
      "cluster-id-availability",
      { clusterInfobaseId, excludeId: excludeId ?? null },
    ],
    queryFn: () =>
      api<ClusterIdAvailability>(`/api/v1/infobases/cluster-id-availability?${qs.toString()}`),
    enabled: enabled && isGuid,
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

export function useReassignInfobase() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: ({ id, targetTenantId }: { id: string; targetTenantId: string }) =>
      api<InfobaseDetail>(`/api/v1/infobases/${id}/reassign`, {
        method: "POST",
        body: { targetTenantId },
      }),
    onSuccess: () => {
      void qc.invalidateQueries({ queryKey: infobasesQueryKey });
      // Счётчики баз на странице клиентов зависят от привязки — обновляем их тоже.
      void qc.invalidateQueries({ queryKey: tenantsQueryKey });
    },
  });
}

export function useDeleteInfobase() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (id: string) => api<null>(`/api/v1/infobases/${id}`, { method: "DELETE" }),
    onSuccess: () => {
      void qc.invalidateQueries({ queryKey: infobasesQueryKey });
    },
  });
}
