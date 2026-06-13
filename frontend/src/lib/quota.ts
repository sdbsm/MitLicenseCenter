import type { StatusBadgeVariant } from "@/components/ui/StatusBadge";

/**
 * Единый источник правил визуального языка квоты лицензий (MLC-122 / R6).
 *
 * Пороги severity намеренно вынесены в именованные константы — они не должны
 * расползаться по кодовой базе. Все экраны (/tenants, /tenants/:id, /reports,
 * дашборд) импортируют хелперы отсюда.
 */

export const QUOTA_WARNING_THRESHOLD = 75;
export const QUOTA_DANGER_THRESHOLD = 90;

export type QuotaSeverity = "ok" | "warning" | "danger";

/**
 * Процент потребления (0–100+).
 * limit <= 0 означает «безлимит» → 0 (нет смысла считать процент).
 */
export function quotaPercent(consumed: number, limit: number): number {
  if (limit <= 0) return 0;
  return Math.round((consumed / limit) * 100);
}

/**
 * Уровень критичности квоты.
 * - limit <= 0 → "ok" (безлимит, без акцента).
 * - percent >= QUOTA_DANGER_THRESHOLD  → "danger".
 * - percent >= QUOTA_WARNING_THRESHOLD → "warning".
 * - иначе → "ok".
 */
export function quotaSeverity(consumed: number, limit: number): QuotaSeverity {
  if (limit <= 0) return "ok";
  const pct = quotaPercent(consumed, limit);
  if (pct >= QUOTA_DANGER_THRESHOLD) return "danger";
  if (pct >= QUOTA_WARNING_THRESHOLD) return "warning";
  return "ok";
}

/**
 * severity → вариант StatusBadge.
 * "ok" → "neutral" (нет акцента, бейдж не кричит).
 */
export function severityToStatusBadgeVariant(severity: QuotaSeverity): StatusBadgeVariant {
  if (severity === "danger") return "danger";
  if (severity === "warning") return "warning";
  return "neutral";
}

/**
 * severity → Tailwind-класс для Progress (цвет индикатора).
 * Пустая строка = цвет по умолчанию (ok / безлимит).
 */
export function severityToProgressClass(severity: QuotaSeverity): string {
  if (severity === "danger") return "[&>[data-slot=progress-indicator]]:bg-rose-500";
  if (severity === "warning") return "[&>[data-slot=progress-indicator]]:bg-amber-500";
  return "";
}

/**
 * Удобный объединённый хелпер — возвращает всё, что нужно для рендера квоты.
 */
export function quotaDisplay(consumed: number, limit: number) {
  const percent = quotaPercent(consumed, limit);
  const severity = quotaSeverity(consumed, limit);
  return {
    percent,
    severity,
    badgeVariant: severityToStatusBadgeVariant(severity),
    progressClass: severityToProgressClass(severity),
  };
}
