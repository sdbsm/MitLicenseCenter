// AuditActionType / AuditReason приходят как строковые имена (см. backend
// JsonStringEnumConverter). Список значений зеркалит Domain/Audit/AuditActionType
// и должен обновляться, если в Stage 3 enum пополнится.
export type AuditActionType =
  | "TenantCreated"
  | "TenantUpdated"
  | "TenantDeleted"
  | "InfobaseCreated"
  | "InfobaseUpdated"
  | "InfobaseDeleted"
  | "PublicationCreated"
  | "PublicationUpdated"
  | "PublicationDeleted"
  | "AdminLoggedIn"
  | "AdminLoggedOut"
  | "AdminPasswordChanged"
  | "SettingChanged";

export const AUDIT_ACTION_TYPES: readonly AuditActionType[] = [
  "TenantCreated",
  "TenantUpdated",
  "TenantDeleted",
  "InfobaseCreated",
  "InfobaseUpdated",
  "InfobaseDeleted",
  "PublicationCreated",
  "PublicationUpdated",
  "PublicationDeleted",
  "AdminLoggedIn",
  "AdminLoggedOut",
  "AdminPasswordChanged",
  "SettingChanged",
] as const;

export type AuditReason = "LimitExceeded" | "ManualByAdmin";

export interface AuditEntry {
  id: string;
  timestamp: string;
  actionType: AuditActionType;
  reason: AuditReason | null;
  initiator: string;
  description: string;
  tenantId: string | null;
}

export interface AuditPagedResponse {
  items: AuditEntry[];
  total: number;
  page: number;
  pageSize: number;
}

export interface AuditFilters {
  actionType: AuditActionType | null;
  tenantId: string | null;
  from: string | null;
  to: string | null;
  page: number;
  pageSize: AuditPageSize;
}

export const AUDIT_PAGE_SIZES = [25, 50, 100] as const;
export type AuditPageSize = (typeof AUDIT_PAGE_SIZES)[number];
export const DEFAULT_AUDIT_PAGE_SIZE: AuditPageSize = 50;
