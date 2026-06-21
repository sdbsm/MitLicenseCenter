import { format } from "date-fns";
import { ru } from "date-fns/locale";
import type { StatusBadgeVariant } from "@/components/ui/StatusBadge";
import type { InvestigationStatus, ReportSeverity } from "./types";

/**
 * Утилиты раздела «Расследование» (MLC-243):
 * маппинг статусов/серьёзности на варианты StatusBadge, форматтеры дат.
 */

/** Маппинг статуса дела → вариант StatusBadge. */
export const INVESTIGATION_STATUS_VARIANT: Record<InvestigationStatus, StatusBadgeVariant> = {
  Collecting: "info",
  Analyzing: "info",
  Completed: "success",
  Interrupted: "warning",
  Failed: "danger",
};

/** Маппинг серьёзности отчёта → вариант StatusBadge. */
export const REPORT_SEVERITY_VARIANT: Record<ReportSeverity, StatusBadgeVariant> = {
  None: "neutral",
  Info: "info",
  Warning: "warning",
};

/** Форматирует ISO-дату в «dd.MM.yyyy HH:mm». */
export function fmtDate(iso: string): string {
  return format(new Date(iso), "dd.MM.yyyy HH:mm", { locale: ru });
}

/** Форматирует секунды в строку «X,X с» / «X мин Y с». */
export function fmtSeconds(seconds: number): string {
  if (seconds < 60) return `${seconds.toFixed(1)} с`;
  const m = Math.floor(seconds / 60);
  const s = (seconds % 60).toFixed(0);
  return `${m} мин ${s} с`;
}
