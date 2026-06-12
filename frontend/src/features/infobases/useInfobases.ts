import { useQuery } from "@tanstack/react-query";
import { api } from "@/lib/api";
import { useInvalidatingMutation } from "@/lib/useInvalidatingMutation";
import { tenantsQueryKey } from "@/features/tenants/useTenants";
import {
  infobaseListResponseSchema,
  type ClusterIdAvailability,
  type CreateInfobaseInput,
  type InfobaseDetail,
  type UpdateInfobaseInput,
} from "./types";

export const infobasesQueryKey = ["infobases"] as const;

export const INFOBASES_PAGE_SIZE = 25;

const GUID_PATTERN =
  /^[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}$/;

// Серверная пагинация (MLC-015). queryKey включает фильтры и page/pageSize; префикс
// остаётся `["infobases"]`, поэтому мутации инвалидируют все страницы/фильтры разом.
// publishStatus (MLC-090) — фильтр по статусу публикации; пусто/null → без фильтра.
export function useInfobases(
  tenantId?: string | null,
  publishStatus?: string | null,
  page = 1,
  pageSize = INFOBASES_PAGE_SIZE
) {
  const qs = new URLSearchParams({ page: String(page), pageSize: String(pageSize) });
  if (tenantId) {
    qs.set("tenantId", tenantId);
  }
  if (publishStatus) {
    qs.set("publishStatus", publishStatus);
  }
  return useQuery({
    queryKey: [
      ...infobasesQueryKey,
      { tenantId: tenantId ?? null, publishStatus: publishStatus ?? null, page, pageSize },
    ],
    queryFn: () =>
      api(`/api/v1/infobases?${qs.toString()}`, { schema: infobaseListResponseSchema }),
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
  return useInvalidatingMutation({
    mutationFn: (input: CreateInfobaseInput) =>
      api<InfobaseDetail>("/api/v1/infobases", { method: "POST", body: input }),
    invalidate: infobasesQueryKey,
  });
}

export function useUpdateInfobase() {
  return useInvalidatingMutation({
    mutationFn: ({ id, input }: { id: string; input: UpdateInfobaseInput }) =>
      api<InfobaseDetail>(`/api/v1/infobases/${id}`, { method: "PUT", body: input }),
    invalidate: infobasesQueryKey,
  });
}

export function useReassignInfobase() {
  return useInvalidatingMutation({
    mutationFn: ({ id, targetTenantId }: { id: string; targetTenantId: string }) =>
      api<InfobaseDetail>(`/api/v1/infobases/${id}/reassign`, {
        method: "POST",
        body: { targetTenantId },
      }),
    // Счётчики баз на странице клиентов зависят от привязки — обновляем их тоже.
    invalidate: [infobasesQueryKey, tenantsQueryKey],
  });
}

// MLC-113 (UX-43): unpublishFromIis=true добавляет ?unpublishFromIis=true — бэкенд
// СНАЧАЛА снимает публикацию из IIS через webinst -delete и при сбое возвращает 409,
// не удаляя инфобазу (защита от молчаливого сиротства публикации в IIS).
export function useDeleteInfobase() {
  return useInvalidatingMutation({
    mutationFn: ({ id, unpublishFromIis }: { id: string; unpublishFromIis?: boolean }) =>
      api<null>(`/api/v1/infobases/${id}${unpublishFromIis ? "?unpublishFromIis=true" : ""}`, {
        method: "DELETE",
      }),
    invalidate: infobasesQueryKey,
  });
}
