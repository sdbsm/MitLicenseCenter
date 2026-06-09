import { z } from "zod";
import { omittable } from "@/lib/apiSchema";

/**
 * Бэкапы баз SQL (`/api/v1/backups`, MLC-078, ADR-27). Критичная граница (ADR-10.1 /
 * MLC-016): по статусу и причине провала оператор судит, есть ли у него свежая копия
 * базы, — ответ проходит runtime-валидацию схемой, типы выводятся из схем (`z.infer`).
 *
 * `status`/`failureReason` приходят строкой (`JsonStringEnumConverter`). Незнакомое
 * значение из будущей версии бэкенда НЕ роняет весь список (образец «family» в
 * performance/types): после строгого `z.enum` стоит string-ветка, пропускающая сырую
 * строку, — UI деградирует к нейтральному бейджу с сырым именем (lookup `?? neutral`
 * в `backupFormat.ts`, i18n с `defaultValue`), но не-строка по-прежнему отвергается.
 *
 * **Сериализация бэкенда опускает `null`-поля** (`JsonIgnoreCondition.WhenWritingNull`,
 * урок [[api-omits-null-fields]]): у Queued-строки `startedAtUtc`/`completedAtUtc`/
 * `filePath`/`fileSizeBytes`/`errorMessage` не приходят как `null`, а отсутствуют —
 * поэтому все пять объявлены через `omittable()`; `.nullable()` уронил бы границу.
 */
export const BACKUP_STATUSES = ["Queued", "Running", "Succeeded", "Failed"] as const;
export type BackupStatus = (typeof BACKUP_STATUSES)[number];

export const BACKUP_FAILURE_REASONS = [
  "None",
  "InsufficientSpace",
  "EstimateUnavailable",
  "PermissionDenied",
  "BackupFailed",
  "Interrupted",
] as const;
export type BackupFailureReason = (typeof BACKUP_FAILURE_REASONS)[number];

export const backupStatusSchema = z
  .enum(BACKUP_STATUSES)
  .or(z.string().transform((value) => value as BackupStatus));

export const backupFailureReasonSchema = z
  .enum(BACKUP_FAILURE_REASONS)
  .or(z.string().transform((value) => value as BackupFailureReason));

export const backupSummarySchema = z.object({
  id: z.string(),
  infobaseId: z.string(),
  databaseServer: z.string(),
  databaseName: z.string(),
  status: backupStatusSchema,
  requestedBy: z.string(),
  requestedAtUtc: z.string(),
  startedAtUtc: omittable(z.string()),
  completedAtUtc: omittable(z.string()),
  filePath: omittable(z.string()),
  fileSizeBytes: omittable(z.number()),
  // None у Queued/Running/Succeeded; конкретная причина — только у Failed.
  failureReason: backupFailureReasonSchema,
  errorMessage: omittable(z.string()),
});

export const backupListSchema = z.array(backupSummarySchema);

export type BackupSummary = z.infer<typeof backupSummarySchema>;
