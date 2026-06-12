// AuditActionType / AuditReason приходят как строковые имена (см. backend
// JsonStringEnumConverter). Union-тип — ПОЛНОЕ зеркало Domain/Audit/AuditActionType
// (включая legacy/frozen действия), чтобы исторические строки рендерились в колонке.
// Обновляется при пополнении enum. AUDIT_ACTION_TYPES ниже — подмножество только
// активно пишущихся действий (опции фильтра + валидация URL).
export type AuditActionType =
  | "TenantCreated"
  | "TenantUpdated"
  | "TenantDeleted"
  | "InfobaseCreated"
  | "InfobaseUpdated"
  | "InfobaseDeleted"
  | "InfobaseReassigned"
  | "PublicationCreated"
  | "PublicationUpdated"
  | "PublicationDeleted"
  | "PublicationUnpublished"
  | "AdminLoggedIn"
  | "AdminLoggedOut"
  | "AdminPasswordChanged"
  | "LoginFailed"
  | "UserCreated"
  | "UserDisabled"
  | "UserPasswordReset"
  | "UserEnabled"
  | "UserRoleChanged"
  | "SessionKilled"
  | "LimitChanged"
  | "PublicationDriftDetected"
  | "PublicationReconciled"
  | "PublicationPublished"
  | "PublicationPlatformChanged"
  | "ClusterAdapterCircuitOpened"
  | "ClusterAdapterCircuitClosed"
  | "SettingChanged"
  | "AuditLogsPurged"
  | "BackupRequested"
  | "BackupSucceeded"
  | "BackupFailed"
  | "BackupDeleted"
  | "BackupsPurged"
  | "IisApplicationPoolRecycled"
  | "IisApplicationPoolStarted"
  | "IisApplicationPoolStopped"
  | "IisSiteStarted"
  | "IisSiteStopped"
  | "IisSiteRestarted"
  | "IisReset"
  | "IisStopped"
  | "IisStarted";

// Активно пишущиеся действия для фильтра. Исключены legacy/frozen
// (drift/reconcile-цикл и circuit-breaker — новые строки с ними не пишутся);
// их переводы в i18n сохранены только для рендера исторических строк в колонке.
export const AUDIT_ACTION_TYPES: readonly AuditActionType[] = [
  "TenantCreated",
  "TenantUpdated",
  "TenantDeleted",
  "InfobaseCreated",
  "InfobaseUpdated",
  "InfobaseDeleted",
  "InfobaseReassigned",
  "PublicationCreated",
  "PublicationUpdated",
  "PublicationDeleted",
  "PublicationPublished",
  "PublicationUnpublished",
  "PublicationPlatformChanged",
  "AdminLoggedIn",
  "AdminLoggedOut",
  "AdminPasswordChanged",
  "LoginFailed",
  "UserCreated",
  "UserDisabled",
  "UserPasswordReset",
  "UserEnabled",
  "UserRoleChanged",
  "SessionKilled",
  "LimitChanged",
  "SettingChanged",
  "AuditLogsPurged",
  "BackupRequested",
  "BackupSucceeded",
  "BackupFailed",
  "BackupDeleted",
  "BackupsPurged",
  "IisApplicationPoolRecycled",
  "IisApplicationPoolStarted",
  "IisApplicationPoolStopped",
  "IisSiteStarted",
  "IisSiteStopped",
  "IisSiteRestarted",
  "IisReset",
  "IisStopped",
  "IisStarted",
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
