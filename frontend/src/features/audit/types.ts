import { z } from "zod";
import { omittable } from "@/lib/apiSchema";

/**
 * AuditActionType / AuditReason приходят как строковые имена (backend
 * JsonStringEnumConverter). Union-тип — ПОЛНОЕ зеркало Domain/Audit/AuditActionType
 * (включая legacy/frozen действия), чтобы исторические строки рендерились в колонке.
 * Обновляется при пополнении enum. AUDIT_ACTION_TYPES ниже — подмножество только
 * активно пишущихся действий (опции фильтра + валидация URL).
 *
 * Zod-схемы (MLC-132, FE-09): enum-поля описаны через z.enum + строковую ветку —
 * незнакомое будущее значение не роняет весь список (деградирует к сырой строке).
 * Backend опускает null-поля (ADR-32): reason=null → ключ отсутствует → omittable();
 * tenantId=null → ключ отсутствует → omittable().
 */
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

// Zod-схемы: enum-поля forward-compatible (строковая ветка пропускает неизвестное значение).
// Инвариант «enum аудита заморожен» (CLAUDE.md): int-значения не переназначаются,
// новые действия получают новые числа — здесь влияет только на строковое имя на wire.
export const auditActionTypeSchema = z
  .enum([...AUDIT_ACTION_TYPES] as [AuditActionType, ...AuditActionType[]])
  .or(z.string().transform((v) => v as AuditActionType));

export const auditReasonSchema = z
  .enum(["LimitExceeded", "ManualByAdmin"] as const)
  .or(z.string().transform((v) => v as AuditReason));

// Backend опускает null-поля (ADR-32):
// reason=null (большинство действий без причины) → ключ отсутствует.
// tenantId=null (системные действия без клиента) → ключ отсутствует.
export const auditEntrySchema = z.object({
  id: z.string(),
  timestamp: z.string(),
  actionType: auditActionTypeSchema,
  reason: omittable(auditReasonSchema),
  initiator: z.string(),
  description: z.string(),
  tenantId: omittable(z.string()),
});

export const auditPagedResponseSchema = z.object({
  items: z.array(auditEntrySchema),
  total: z.number(),
  page: z.number(),
  pageSize: z.number(),
});

export type AuditEntry = z.infer<typeof auditEntrySchema>;
export type AuditPagedResponse = z.infer<typeof auditPagedResponseSchema>;

export interface AuditFilters {
  actionType: AuditActionType | null;
  tenantId: string | null;
  from: string | null;
  to: string | null;
  search: string | null;
  initiator: string | null;
  page: number;
  pageSize: AuditPageSize;
}

export const AUDIT_PAGE_SIZES = [25, 50, 100] as const;
export type AuditPageSize = (typeof AUDIT_PAGE_SIZES)[number];
export const DEFAULT_AUDIT_PAGE_SIZE: AuditPageSize = 50;
