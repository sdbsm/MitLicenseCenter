import { useQuery } from "@tanstack/react-query";
import { api } from "@/lib/api";
import { useInvalidatingMutation } from "@/lib/useInvalidatingMutation";
import { tenantsQueryKey } from "@/features/tenants/useTenants";
import {
  clusterIdAvailabilitySchema,
  infobaseBulkIdsResponseSchema,
  infobaseDetailSchema,
  infobaseListResponseSchema,
  type ClusterIdAvailability,
  type CreateInfobaseInput,
  type InfobaseBulkIdsResponse,
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
// notInCluster (MLC-150) — серверный фильтр «не найдена в кластере» (обратный дрейф):
// true добавляет ?notInCluster=true; BE отдаёт clusterAvailable как признак доступности
// снапшота RAS (при недоступном — пустой набор + clusterAvailable:false, а не ложный «0»).
// search (MLC-181a) — подстрочный поиск по имени базы и имени БД на сервере (plain
// Contains→LIKE); пустой/undefined не добавляется в query. Включён в queryKey, чтобы
// кэш различал выборки.
export function useInfobases(
  tenantId?: string | null,
  publishStatus?: string | null,
  notInCluster = false,
  page = 1,
  pageSize = INFOBASES_PAGE_SIZE,
  search?: string
) {
  const term = search?.trim() ?? "";
  const qs = new URLSearchParams({ page: String(page), pageSize: String(pageSize) });
  if (tenantId) {
    qs.set("tenantId", tenantId);
  }
  if (publishStatus) {
    qs.set("publishStatus", publishStatus);
  }
  if (notInCluster) {
    qs.set("notInCluster", "true");
  }
  if (term) {
    qs.set("search", term);
  }
  return useQuery({
    queryKey: [
      ...infobasesQueryKey,
      {
        tenantId: tenantId ?? null,
        publishStatus: publishStatus ?? null,
        notInCluster,
        page,
        pageSize,
        search: term,
      },
    ],
    queryFn: () =>
      api(`/api/v1/infobases?${qs.toString()}`, { schema: infobaseListResponseSchema }),
    // Не моргаем скелетоном при смене страницы/фильтра — показываем предыдущие данные.
    placeholderData: (prev) => prev,
  });
}

// MLC-181c — облегчённый id-набор для bulk «Выбрать все N по фильтру». Строит ТОТ ЖЕ
// query, что и useInfobases (tenantId/publishStatus/notInCluster/search), БЕЗ page/pageSize,
// и дёргает /api/v1/infobases/ids по требованию (кнопка в bulk-баре) — не поллинг, не useQuery.
// BE по тому же фильтру отдаёт только пригодные для bulk строки (с публикацией); capped=true ⇒
// набор сверх кэпа, выбор не наполняем.
export function fetchInfobaseBulkIds(
  tenantId?: string | null,
  publishStatus?: string | null,
  notInCluster = false,
  search?: string
): Promise<InfobaseBulkIdsResponse> {
  const qs = new URLSearchParams();
  if (tenantId) {
    qs.set("tenantId", tenantId);
  }
  if (publishStatus) {
    qs.set("publishStatus", publishStatus);
  }
  if (notInCluster) {
    qs.set("notInCluster", "true");
  }
  const term = search?.trim() ?? "";
  if (term) {
    qs.set("search", term);
  }
  const query = qs.toString();
  return api(`/api/v1/infobases/ids${query ? `?${query}` : ""}`, {
    schema: infobaseBulkIdsResponseSchema,
  });
}

// Точечная проверка занятости базы кластера (MLC-015). takenByTenantName=null →
// бэкенд опускает ключ (WhenWritingNull) → clusterIdAvailabilitySchema с omittable().
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
      api<ClusterIdAvailability>(`/api/v1/infobases/cluster-id-availability?${qs.toString()}`, {
        schema: clusterIdAvailabilitySchema,
      }),
    enabled: enabled && isGuid,
  });
}

// MLC-132: create/update/reassign возвращают InfobaseDetailResponse — схема подключена.
// Это позволяет поймать дрейф контракта infobase/publication при обновлении бэкенда.
export function useCreateInfobase() {
  return useInvalidatingMutation({
    mutationFn: (input: CreateInfobaseInput) =>
      api<InfobaseDetail>("/api/v1/infobases", {
        method: "POST",
        body: input,
        schema: infobaseDetailSchema,
      }),
    // Создание базы меняет счётчик баз клиента на странице /tenants — обновляем и его.
    invalidate: [infobasesQueryKey, tenantsQueryKey],
  });
}

export function useUpdateInfobase() {
  return useInvalidatingMutation({
    mutationFn: ({ id, input }: { id: string; input: UpdateInfobaseInput }) =>
      api<InfobaseDetail>(`/api/v1/infobases/${id}`, {
        method: "PUT",
        body: input,
        schema: infobaseDetailSchema,
      }),
    invalidate: infobasesQueryKey,
  });
}

export function useReassignInfobase() {
  return useInvalidatingMutation({
    mutationFn: ({ id, targetTenantId }: { id: string; targetTenantId: string }) =>
      api<InfobaseDetail>(`/api/v1/infobases/${id}/reassign`, {
        method: "POST",
        body: { targetTenantId },
        schema: infobaseDetailSchema,
      }),
    // Счётчики баз на странице клиентов зависят от привязки — обновляем их тоже.
    invalidate: [infobasesQueryKey, tenantsQueryKey],
  });
}

// MLC-113 (UX-43): unpublishFromIis=true добавляет ?unpublishFromIis=true — бэкенд
// СНАЧАЛА снимает публикацию из IIS через webinst -delete и при сбое возвращает 409,
// не удаляя инфобазу (защита от молчаливого сиротства публикации в IIS).
// DELETE возвращает 204 No Content (null body) — схема не нужна.
export function useDeleteInfobase() {
  return useInvalidatingMutation({
    mutationFn: ({ id, unpublishFromIis }: { id: string; unpublishFromIis?: boolean }) =>
      api<null>(`/api/v1/infobases/${id}${unpublishFromIis ? "?unpublishFromIis=true" : ""}`, {
        method: "DELETE",
      }),
    // Удаление базы меняет счётчик баз клиента на странице /tenants — обновляем и его.
    invalidate: [infobasesQueryKey, tenantsQueryKey],
  });
}
