import { api } from "@/lib/api";
import { useInvalidatingMutation } from "@/lib/useInvalidatingMutation";
import { infobasesQueryKey } from "@/features/infobases/useInfobases";
import { publicationStatusResponseSchema, type PublicationStatusResponse } from "./types";

// MLC-081: отдельного списка публикаций больше нет — данные публикаций живут во
// вложенном объекте строки списка инфобаз, поэтому мутации инвалидируют ["infobases"]
// (префикс покрывает все страницы/фильтры серверной пагинации).
//
// MLC-132: мутации useCheckStatus/usePublish/useUnpublish/useChangePlatform возвращают
// PublicationStatusResponse — схема подключена. Это позволяет поймать дрейф контракта
// status/checkedAt/details при обновлении бэкенда.

// Read-only проверка факта публикации в IIS. Возвращает свежий статус и
// инвалидирует список (статус уже записан на сервере).
export function useCheckStatus() {
  return useInvalidatingMutation({
    mutationFn: (publicationId: string) =>
      api<PublicationStatusResponse>(`/api/v1/publications/${publicationId}/check`, {
        method: "POST",
        schema: publicationStatusResponseSchema,
      }),
    invalidate: () => [infobasesQueryKey],
  });
}

// (Пере)публикация через webinst. confirm=true снимает гейт перезатирания
// чужой (не webinst) публикации.
export function usePublish() {
  return useInvalidatingMutation({
    mutationFn: (vars: { id: string; confirm: boolean }) =>
      api<PublicationStatusResponse>(`/api/v1/publications/${vars.id}/publish`, {
        method: "POST",
        body: { confirm: vars.confirm },
        schema: publicationStatusResponseSchema,
      }),
    invalidate: () => [infobasesQueryKey],
  });
}

// Снятие публикации через webinst -delete (MLC-113). Без тела; после успеха фактический
// статус публикации станет NotPublished. Инвалидирует список инфобаз.
export function useUnpublish() {
  return useInvalidatingMutation({
    mutationFn: (publicationId: string) =>
      api<PublicationStatusResponse>(`/api/v1/publications/${publicationId}/unpublish`, {
        method: "POST",
        schema: publicationStatusResponseSchema,
      }),
    invalidate: () => [infobasesQueryKey],
  });
}

// Смена платформы — правка пути к wsisapi.dll в web.config под новую версию.
export function useChangePlatform() {
  return useInvalidatingMutation({
    mutationFn: (vars: { id: string; platformVersion: string }) =>
      api<PublicationStatusResponse>(`/api/v1/publications/${vars.id}/change-platform`, {
        method: "POST",
        body: { platformVersion: vars.platformVersion },
        schema: publicationStatusResponseSchema,
      }),
    invalidate: () => [infobasesQueryKey],
  });
}
