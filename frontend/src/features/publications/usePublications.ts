import { useQuery } from "@tanstack/react-query";
import { api } from "@/lib/api";
import { useInvalidatingMutation } from "@/lib/useInvalidatingMutation";
import type {
  PublicationListItem,
  PublicationStatusResponse,
  PublicationsBackendListResponse,
} from "./types";

export const publicationsQueryKey = ["publications"] as const;

function flatten(response: PublicationsBackendListResponse): PublicationListItem[] {
  return response.items.map((item) => ({
    id: item.publication.id,
    infobaseId: item.id,
    infobaseName: item.name,
    tenantId: item.tenantId,
    tenantName: item.tenantName,
    siteName: item.publication.siteName,
    virtualPath: item.publication.virtualPath,
    platformVersion: item.publication.platformVersion,
    source: item.publication.source,
    lastCheckStatus: item.publication.lastCheckStatus,
    lastCheckAt: item.publication.lastCheckAt,
    lastCheckDetails: item.publication.lastCheckDetails,
  }));
}

export function usePublications() {
  return useQuery({
    queryKey: publicationsQueryKey,
    queryFn: () => api<PublicationsBackendListResponse>("/api/v1/infobases?page=1&pageSize=200"),
    select: flatten,
    refetchInterval: 60_000,
    refetchOnWindowFocus: true,
    placeholderData: (prev) => prev,
  });
}

// Read-only проверка факта публикации в IIS. Возвращает свежий статус и
// инвалидирует список (статус уже записан на сервере).
export function useCheckStatus() {
  return useInvalidatingMutation({
    mutationFn: (publicationId: string) =>
      api<PublicationStatusResponse>(`/api/v1/publications/${publicationId}/check`, {
        method: "POST",
      }),
    invalidate: () => [publicationsQueryKey],
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
      }),
    invalidate: () => [publicationsQueryKey],
  });
}

// Смена платформы — правка пути к wsisapi.dll в web.config под новую версию.
export function useChangePlatform() {
  return useInvalidatingMutation({
    mutationFn: (vars: { id: string; platformVersion: string }) =>
      api<PublicationStatusResponse>(`/api/v1/publications/${vars.id}/change-platform`, {
        method: "POST",
        body: { platformVersion: vars.platformVersion },
      }),
    invalidate: () => [publicationsQueryKey],
  });
}
