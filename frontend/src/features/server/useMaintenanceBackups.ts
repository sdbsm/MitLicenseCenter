import { useQuery } from "@tanstack/react-query";
import { z } from "zod";
import { api } from "@/lib/api";
import { omittable } from "@/lib/apiSchema";

/**
 * Zod-схема и хук вкладки «Обслуживание» раздела «Сервер» (MLC-216, ADR-54) поверх
 * BE-контракта `GET /api/v1/server/maintenance/backups`. Live-read свежести резервных копий
 * баз из msdb.dbo.backupset (только чтение, без своих таблиц/джоб).
 *
 * Backend опускает null-поля (JsonIgnoreCondition.WhenWritingNull, гоча api-omits-null-fields):
 * lastFull/lastDiff/lastLog приходят либо со значением (ISO-8601 UTC), либо ключ отсутствует —
 * НИКОГДА явным null. Поэтому omittable() (принимает оба варианта, нормализует в null).
 *
 * status держим как z.string() (а не z.enum), чтобы будущий BE-статус не ронял Zod-границу.
 */

// Свежесть бэкапа одной базы. isStale — вычислен бэкендом (нет FULL или последний FULL
// старше ~26ч, BackupFreshnessPolicy); FE его не пересчитывает.
export const databaseBackupFreshnessSchema = z.object({
  databaseName: z.string(),
  lastFullUtc: omittable(z.string()),
  lastDiffUtc: omittable(z.string()),
  lastLogUtc: omittable(z.string()),
  isStale: z.boolean(),
});

// Снимок свежести. status — string ради forward-compat (Ok/PermissionDenied/Unavailable
// сейчас, но новое значение не должно ронять границу). В degraded-ветках databases пуст.
export const backupFreshnessSchema = z.object({
  status: z.string(),
  databases: z.array(databaseBackupFreshnessSchema),
});

export type DatabaseBackupFreshness = z.infer<typeof databaseBackupFreshnessSchema>;
export type BackupFreshness = z.infer<typeof backupFreshnessSchema>;

export const maintenanceBackupsQueryKey = ["server", "maintenance", "backups"] as const;

// ~30с свежести (как статус сервера) — оперативное наблюдение, не риал-тайм.
const STALE_TIME = 30 * 1000;

export function useMaintenanceBackups() {
  return useQuery({
    queryKey: maintenanceBackupsQueryKey,
    queryFn: () => api("/api/v1/server/maintenance/backups", { schema: backupFreshnessSchema }),
    staleTime: STALE_TIME,
  });
}
