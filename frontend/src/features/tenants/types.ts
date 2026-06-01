export interface Tenant {
  id: string;
  name: string;
  maxConcurrentLicenses: number;
  isActive: boolean;
  createdAt: string;
  updatedAt: string | null;
  infobaseCount: number;
}

export interface TenantListResponse {
  items: Tenant[];
  total: number;
  page: number;
  pageSize: number;
}

export interface TenantInput {
  name: string;
  maxConcurrentLicenses: number;
  isActive: boolean;
}
