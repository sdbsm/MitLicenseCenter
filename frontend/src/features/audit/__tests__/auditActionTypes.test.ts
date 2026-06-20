import { describe, expect, it } from "vitest";
import { ru } from "@/i18n";
import { AUDIT_ACTION_TYPES, type AuditActionType } from "../types";

/**
 * Тест-зеркало enum'а аудита (MLC-079, по образцу int-зеркала Backup* из MLC-076/078).
 * Урок MLC-079: backend переименовал слоты 103–106 `Admin*`→`User*` (MLC-060/061), фронт
 * остался на старых i18n-ключах — журнал рендерил сырое `audit.actions.UserCreated`.
 * Здесь словарь `audit.actions` в ru.json сверяется с union-типом в обе стороны:
 * новое действие без перевода и осиротевший перевод без действия валят тест.
 * Record<AuditActionType, true> заставляет TypeScript ругаться, если зеркало
 * отстало от самого union'а.
 */
const ALL_ACTION_TYPES: Record<AuditActionType, true> = {
  TenantCreated: true,
  TenantUpdated: true,
  TenantDeleted: true,
  InfobaseCreated: true,
  InfobaseUpdated: true,
  InfobaseDeleted: true,
  InfobaseReassigned: true,
  PublicationCreated: true,
  PublicationUpdated: true,
  PublicationDeleted: true,
  PublicationUnpublished: true,
  AdminLoggedIn: true,
  AdminLoggedOut: true,
  AdminPasswordChanged: true,
  LoginFailed: true,
  UserCreated: true,
  UserDisabled: true,
  UserPasswordReset: true,
  UserEnabled: true,
  UserRoleChanged: true,
  UserDeleted: true,
  SessionKilled: true,
  LimitChanged: true,
  PublicationDriftDetected: true,
  PublicationReconciled: true,
  PublicationPublished: true,
  PublicationPlatformChanged: true,
  ClusterAdapterCircuitOpened: true,
  ClusterAdapterCircuitClosed: true,
  SettingChanged: true,
  AuditLogsPurged: true,
  BackupRequested: true,
  BackupSucceeded: true,
  BackupFailed: true,
  BackupDeleted: true,
  BackupsPurged: true,
  IisApplicationPoolRecycled: true,
  IisApplicationPoolStarted: true,
  IisApplicationPoolStopped: true,
  IisSiteStarted: true,
  IisSiteStopped: true,
  IisSiteRestarted: true,
  IisReset: true,
  IisStopped: true,
  IisStarted: true,
  RasServiceRegistered: true,
  RasServiceUpdated: true,
  RasServiceStarted: true,
  PerfRecordingStarted: true,
  PerfRecordingStopped: true,
  PerfRecordingDeleted: true,
  OneCServerStarted: true,
  OneCServerStopped: true,
  OneCServerRestarted: true,
  OneCServerAutoRestarted: true,
  OneCServerAutoRestartScheduleChanged: true,
  OneCProcessRestarted: true,
  TechLogCollectionStarted: true,
  TechLogCollectionStopped: true,
  TechLogConfigForceRestored: true,
};

const unionMembers = Object.keys(ALL_ACTION_TYPES).sort();
const i18nKeys = Object.keys(ru.audit.actions).sort();

describe("Зеркало AuditActionType ↔ i18n ↔ фильтр (MLC-079)", () => {
  it("каждый член union имеет перевод в audit.actions, и наоборот", () => {
    expect(i18nKeys).toEqual(unionMembers);
  });

  it("фильтр предлагает все пять User*-действий", () => {
    for (const action of [
      "UserCreated",
      "UserDisabled",
      "UserPasswordReset",
      "UserEnabled",
      "UserRoleChanged",
    ] as const) {
      expect(AUDIT_ACTION_TYPES).toContain(action);
    }
  });

  it("фильтр — без дубликатов", () => {
    expect(new Set(AUDIT_ACTION_TYPES).size).toBe(AUDIT_ACTION_TYPES.length);
  });
});
