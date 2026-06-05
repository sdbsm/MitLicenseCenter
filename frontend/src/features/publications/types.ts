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

export interface PublicationsBackendListItem {
  id: string;
  tenantId: string;
  tenantName: string;
  name: string;
  publication: {
    id: string;
    infobaseId: string;
    siteName: string;
    virtualPath: string;
    platformVersion: string;
    source: PublicationSource;
    physicalPathOverride: string | null;
    createdAt: string;
    updatedAt: string | null;
    lastCheckStatus: PublicationPublishStatus;
    lastCheckAt: string | null;
    lastCheckDetails: string | null;
  };
}

export interface PublicationsBackendListResponse {
  items: PublicationsBackendListItem[];
  total: number;
  page: number;
  pageSize: number;
}
