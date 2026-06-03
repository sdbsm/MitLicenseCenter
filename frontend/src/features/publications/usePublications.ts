import { useMutation, useQuery } from "@tanstack/react-query";
import { api } from "@/lib/api";
import { useInvalidatingMutation } from "@/lib/useInvalidatingMutation";
import type {
  CheckDriftAcceptedResponse,
  DriftStatusResponse,
  PublicationListItem,
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
    enableOData: item.publication.enableOData,
    enableHttpServices: item.publication.enableHttpServices,
    lastDriftStatus: item.publication.lastDriftStatus,
    lastDriftCheckAt: item.publication.lastDriftCheckAt,
    lastDriftDetails: item.publication.lastDriftDetails,
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

export function driftStatusQueryKey(publicationId: string) {
  return ["publication", publicationId, "drift-status"] as const;
}

export function fetchDriftStatus(publicationId: string): Promise<DriftStatusResponse> {
  return api<DriftStatusResponse>(`/api/v1/publications/${publicationId}/drift-status`);
}

export function useCheckDrift() {
  return useMutation({
    mutationFn: (publicationId: string) =>
      api<CheckDriftAcceptedResponse>(`/api/v1/publications/${publicationId}/check-drift`, {
        method: "POST",
      }),
  });
}

export function useReconcile() {
  return useInvalidatingMutation({
    mutationFn: (publicationId: string) =>
      api<DriftStatusResponse>(`/api/v1/publications/${publicationId}/reconcile`, {
        method: "POST",
      }),
    // Ключ drift-статуса зависит от id публикации — резолвим его из переменных мутации.
    invalidate: (publicationId) => [publicationsQueryKey, driftStatusQueryKey(publicationId)],
  });
}
