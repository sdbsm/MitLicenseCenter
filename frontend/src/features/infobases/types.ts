export type InfobaseStatus = "Active" | "Maintenance" | "Suspended";

export interface Publication {
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
}

export interface Infobase {
  id: string;
  tenantId: string;
  name: string;
  clusterInfobaseId: string;
  databaseServer: string;
  databaseName: string;
  status: InfobaseStatus;
  createdAt: string;
  updatedAt: string | null;
}

export interface InfobaseListItem extends Infobase {
  tenantName: string;
  publication: Publication;
}

export interface InfobaseListResponse {
  items: InfobaseListItem[];
  total: number;
  page: number;
  pageSize: number;
}

export interface InfobaseDetail {
  infobase: Infobase;
  publication: Publication;
}

export interface ClusterIdAvailability {
  taken: boolean;
  takenByTenantName: string | null;
}

export interface PublicationInput {
  siteName: string;
  virtualPath: string;
  platformVersion: string;
  enableOData: boolean;
  enableHttpServices: boolean;
  vrdCustomXml: string | null;
  physicalPathOverride: string | null;
}

export interface CreateInfobaseInput {
  tenantId: string;
  name: string;
  clusterInfobaseId: string;
  databaseServer: string;
  databaseName: string;
  status: InfobaseStatus;
  publication: PublicationInput;
}

export interface UpdateInfobaseInput {
  name: string;
  clusterInfobaseId: string;
  databaseServer: string;
  databaseName: string;
  status: InfobaseStatus;
  publication: PublicationInput;
}
