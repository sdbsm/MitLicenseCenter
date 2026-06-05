import type { PublicationPublishStatus } from "./types";

export const PUBLISH_STATUSES: readonly PublicationPublishStatus[] = [
  "Published",
  "NotPublished",
  "Error",
  "Unknown",
];

export interface UrlFilters {
  tenantId: string;
  status: PublicationPublishStatus | "";
}

export function isPublishStatus(value: string): value is PublicationPublishStatus {
  return (PUBLISH_STATUSES as readonly string[]).includes(value);
}

export function parseParams(params: URLSearchParams): UrlFilters {
  const s = params.get("status") ?? "";
  return {
    tenantId: params.get("tenantId") ?? "",
    status: isPublishStatus(s) ? s : "",
  };
}
