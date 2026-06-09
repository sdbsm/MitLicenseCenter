import type { StatusBadgeVariant } from "@/components/ui/StatusBadge";
import type { BackupStatus } from "./types";

/**
 * Чистое форматирование/семантика статусов диалога бэкапов (MLC-078). Без React —
 * покрыто unit-тестами. Цветовая семантика статусов — 06_UI_DESIGN §3.
 */
const BACKUP_STATUS_VARIANT: Record<BackupStatus, StatusBadgeVariant> = {
  Queued: "neutral",
  Running: "info",
  Succeeded: "success",
  Failed: "danger",
};

// Незнакомый статус будущего бэкенда схема пропускает строкой (см. types.ts) —
// деградируем к нейтральному бейджу, не роняя список.
export function backupStatusVariant(status: BackupStatus): StatusBadgeVariant {
  return BACKUP_STATUS_VARIANT[status] ?? "neutral";
}

// Размер файла бэкапа → «—» при отсутствии (Queued/Running/Failed), иначе КБ/МБ/ГБ.
// Сжатый .bak маленькой базы — сотни КБ, больших — десятки ГБ.
export function formatBackupSize(bytes: number | null): string {
  if (bytes === null) return "—";
  const gb = bytes / 1024 ** 3;
  if (gb >= 1) return `${gb.toFixed(1)} ГБ`;
  const mb = bytes / 1024 ** 2;
  if (mb >= 1) return `${mb.toFixed(1)} МБ`;
  return `${Math.round(bytes / 1024)} КБ`;
}
