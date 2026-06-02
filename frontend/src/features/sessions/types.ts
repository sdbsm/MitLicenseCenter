import { z } from "zod";

/**
 * Снимок активных сеансов (`GET /api/v1/sessions/snapshot`). Критичная граница
 * (MLC-016): данные снимка (`consumesLicense`, `durationSeconds`, дескрипторы
 * сеанса) питают операционную картину over-limit/kill, поэтому ответ проходит
 * runtime-валидацию. Типы выводятся из схем (`z.infer`) — единый источник правды.
 */
export const sessionSnapshotEntrySchema = z.object({
  sessionId: z.string(),
  clusterInfobaseId: z.string(),
  tenantId: z.string(),
  tenantName: z.string(),
  infobaseName: z.string(),
  appId: z.string(),
  userName: z.string(),
  host: z.string(),
  consumesLicense: z.boolean(),
  startedAt: z.string(),
  durationSeconds: z.number(),
});

export const sessionsSnapshotResponseSchema = z.object({
  items: z.array(sessionSnapshotEntrySchema),
  capturedAt: z.string(),
  tookMs: z.number(),
  source: z.string(),
});

export type SessionSnapshotEntry = z.infer<typeof sessionSnapshotEntrySchema>;
export type SessionsSnapshotResponse = z.infer<typeof sessionsSnapshotResponseSchema>;
