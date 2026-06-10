import type { InfobaseListItem } from "@/features/infobases/types";

export type PublicationPublishStatus = "Unknown" | "Published" | "NotPublished" | "Error";
export type PublicationSource = "Unknown" | "Webinst" | "Configurator";

export interface PublicationListItem {
  id: string;
  infobaseId: string;
  infobaseName: string;
  tenantId: string;
  tenantName: string;
  siteName: string;
  virtualPath: string;
  platformVersion: string;
  source: PublicationSource;
  lastCheckStatus: PublicationPublishStatus;
  lastCheckAt: string | null;
  lastCheckDetails: string | null;
}

export interface PublicationStatusResponse {
  status: PublicationPublishStatus;
  checkedAt: string | null;
  details: string | null;
}

/**
 * Плоское представление публикации из строки списка инфобаз (MLC-081): после слияния
 * страниц источник данных один — `GET /api/v1/infobases`, а диалоги публикации/смены
 * платформы и bulk-операции продолжают работать с `PublicationListItem`.
 */
export function toPublicationListItem(item: InfobaseListItem): PublicationListItem {
  return {
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
    lastCheckAt: item.publication.lastCheckAt ?? null,
    lastCheckDetails: item.publication.lastCheckDetails ?? null,
  };
}
