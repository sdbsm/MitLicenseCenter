import type { StatusBadgeVariant } from "@/components/ui/StatusBadge";

/**
 * Единый источник правил визуального языка квоты лицензий (MLC-122 / R6).
 *
 * Пороги severity намеренно вынесены в именованные константы — они не должны
 * расползаться по кодовой базе. Все экраны (/tenants, /tenants/:id, «Сеансы» —
 * виды «По клиентам»/«Использование за период», дашборд) импортируют хелперы отсюда.
 */

export const QUOTA_WARNING_THRESHOLD = 75;
export const QUOTA_DANGER_THRESHOLD = 90;

export type QuotaSeverity = "ok" | "warning" | "danger";

/**
 * Текстовый ярлык состояния квоты — по ФАКТУ consumed vs limit (MLC-188), а НЕ по
 * цвету-severity. Превышение — только когда потребление строго больше лимита; равенство
 * (N из N) — «лимит достигнут», не «превышение».
 * - "exceeded"  → consumed > limit (напр. 11 из 10);
 * - "atLimit"   → consumed === limit (10 из 10);
 * - "nearLimit" → ниже лимита, но severity warning/danger (≥75%, < limit);
 * - null        → безлимит или потребление ниже warning-порога (ярлык не показываем).
 */
export type QuotaLabel = "exceeded" | "atLimit" | "nearLimit";

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
 * Ярлык состояния квоты по факту consumed vs limit (MLC-188). Цвет-severity (≥90% danger)
 * не путать с ярлыком: «превышение» только при consumed > limit, равенство — «достигнут».
 */
export function quotaLabel(consumed: number, limit: number): QuotaLabel | null {
  if (limit <= 0) return null;
  if (consumed > limit) return "exceeded";
  if (consumed === limit) return "atLimit";
  if (quotaSeverity(consumed, limit) === "ok") return null;
  return "nearLimit";
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
    label: quotaLabel(consumed, limit),
    badgeVariant: severityToStatusBadgeVariant(severity),
    progressClass: severityToProgressClass(severity),
  };
}
