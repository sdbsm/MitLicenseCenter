import { z } from "zod";

/**
 * Снимок активных сеансов (`GET /api/v1/sessions/snapshot`). Критичная граница
 * (MLC-016): данные снимка (`licenseStatus`, `durationSeconds`, дескрипторы
 * сеанса) питают операционную картину over-limit/kill, поэтому ответ проходит
 * runtime-валидацию. Типы выводятся из схем (`z.infer`) — единый источник правды.
 *
 * ADR-48 (MLC-166): потребление лицензии — факт `rac --licenses`, трёхсостояние
 * `licenseStatus` (строкой через `JsonStringEnumConverter`): Consuming/NotConsuming/
 * Pending. `licenseFactAvailable` на уровне ответа: false ⇒ баннер «данные о лицензиях
 * недоступны». Оба поля — non-nullable на бэкенде; `.default()` лишь страхует от
 * отсутствия ключа (parity-резерв).
 */
export const licenseStatusSchema = z.enum(["Consuming", "NotConsuming", "Pending"]);

export const sessionSnapshotEntrySchema = z.object({
  sessionId: z.string(),
  clusterInfobaseId: z.string(),
  tenantId: z.string(),
  tenantName: z.string(),
  infobaseName: z.string(),
  appId: z.string(),
  userName: z.string(),
  host: z.string(),
  licenseStatus: licenseStatusSchema,
  startedAt: z.string(),
  durationSeconds: z.number(),
});

export const sessionsSnapshotResponseSchema = z.object({
  items: z.array(sessionSnapshotEntrySchema),
  capturedAt: z.string(),
  tookMs: z.number(),
  source: z.string(),
  licenseFactAvailable: z.boolean().default(false),
});

export type LicenseStatus = z.infer<typeof licenseStatusSchema>;
export type SessionSnapshotEntry = z.infer<typeof sessionSnapshotEntrySchema>;
export type SessionsSnapshotResponse = z.infer<typeof sessionsSnapshotResponseSchema>;
