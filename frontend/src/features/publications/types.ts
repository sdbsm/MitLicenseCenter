export type PublicationDriftStatus = "InSync" | "Drift" | "Missing" | "Error";

export interface PublicationListItem {
  id: string;
  infobaseId: string;
  infobaseName: string;
  tenantId: string;
  tenantName: string;
  siteName: string;
  virtualPath: string;
  platformVersion: string;
  enableOData: boolean;
  enableHttpServices: boolean;
  lastDriftStatus: PublicationDriftStatus;
  lastDriftCheckAt: string | null;
  lastDriftDetails: string | null;
}

export interface DriftStatusResponse {
  status: PublicationDriftStatus;
  checkedAt: string | null;
  details: string | null;
}

export interface CheckDriftAcceptedResponse {
  correlationId: string;
  publicationId: string;
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
    enableOData: boolean;
    enableHttpServices: boolean;
    vrdCustomXml: string | null;
    physicalPathOverride: string | null;
    createdAt: string;
    updatedAt: string | null;
    lastDriftStatus: PublicationDriftStatus;
    lastDriftCheckAt: string | null;
    lastDriftDetails: string | null;
  };
}

export interface PublicationsBackendListResponse {
  items: PublicationsBackendListItem[];
  total: number;
  page: number;
  pageSize: number;
}
