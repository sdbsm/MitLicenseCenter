import { useQuery } from "@tanstack/react-query";
import { api } from "@/lib/api";
import { useInvalidatingMutation } from "@/lib/useInvalidatingMutation";
import {
  backupEstimateSchema,
  backupsPagedSchema,
  backupSummarySchema,
  type BackupSummary,
} from "./types";

// Ключ всегда параметризован инфобазой (для id=null — плейсхолдер), чтобы НЕ коллидировать
// между диалогами разных баз и с disabled-запросом закрытого диалога (урок MLC-071:
// общий ключ подсовывал закрытому диалогу чужие данные из кэша).
export const backupsQueryKey = (infobaseId: string | null) =>
  ["backups", infobaseId ?? "__none__"] as const;

/**
 * Бэкапы одной инфобазы (MLC-078, ADR-27). Чтение = Viewer. Поллим 5с, пока диалог открыт
 * (`enabled` по id) — статус Queued→Running→Succeeded двигает оркестратор на бэкенде, и
 * прогресс должен быть виден без ручного обновления. Свежие сверху (порядок задаёт бэкенд).
 * Схема — критичная граница (MLC-016). Эндпоинт пагинирован (BE-17): бэкапы одной инфобазы
 * ограничены keep-latest retention, поэтому запрашиваем одну страницу с запасом (pageSize=100)
 * и читаем `.items` — отдельной UI-листалки в диалоге нет.
 */
export function useBackups(infobaseId: string | null) {
  return useQuery({
    queryKey: backupsQueryKey(infobaseId),
    queryFn: () =>
      api(`/api/v1/backups?infobaseId=${infobaseId}&page=1&pageSize=100`, {
        schema: backupsPagedSchema,
      }),
    enabled: infobaseId !== null,
    refetchInterval: 5_000,
    placeholderData: (prev) => prev,
  });
}

// Предпоказ disk-guard ДО запуска (MLC-183): свободное место + оценка размера бэкапа. Чтение =
// Viewer. БЕЗ поллинга (в отличие от useBackups) — оценка статична, обновлять каждые 5с незачем;
// диалог запрашивает её один раз при открытии. Ключ обособлен per-infobase. Схема — граница MLC-016.
export function useBackupEstimate(infobaseId: string | null) {
  return useQuery({
    queryKey: ["backup-estimate", infobaseId] as const,
    queryFn: () =>
      api(`/api/v1/backups/estimate?infobaseId=${infobaseId}`, {
        schema: backupEstimateSchema,
      }),
    enabled: infobaseId !== null,
  });
}

// Постановка бэкапа в очередь (Viewer — операторская кнопка, ADR-27). 409 BACKUP_ACTIVE /
// BACKUP_FOLDER_NOT_CONFIGURED обрабатывает вызывающий через matchConflictCode.
export function useStartBackup() {
  return useInvalidatingMutation<BackupSummary, string>({
    mutationFn: (infobaseId) =>
      api<BackupSummary>("/api/v1/backups", {
        method: "POST",
        body: { infobaseId },
        schema: backupSummarySchema,
      }),
    invalidate: (infobaseId) => backupsQueryKey(infobaseId),
  });
}

// Удаление бэкапа (Admin): бэкенд сначала сносит файл server-side, затем строку.
// 409 BACKUP_DELETE_FAILED — файл не удалился, запись сохранена; 409 BACKUP_ACTIVE — Running.
export function useDeleteBackup() {
  return useInvalidatingMutation<null, { id: string; infobaseId: string }>({
    mutationFn: ({ id }) => api<null>(`/api/v1/backups/${id}`, { method: "DELETE" }),
    invalidate: ({ infobaseId }) => backupsQueryKey(infobaseId),
  });
}
