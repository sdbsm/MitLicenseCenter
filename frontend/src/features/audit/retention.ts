const MS_PER_DAY = 24 * 60 * 60 * 1000;

// PR 4.3: возвращает true если UI-filter `from` (YYYY-MM-DD) указывает на дату
// раньше cutoff'а retention. Defensive guards для empty/invalid input — banner
// НЕ показывается на тривиальном/битом state.
export function isFilterBeyondRetention(
  fromYmd: string | null,
  retentionDays: number | null | undefined,
  nowUtc: Date
): boolean {
  if (!fromYmd) return false;
  if (retentionDays == null || retentionDays <= 0) return false;
  const fromDate = new Date(`${fromYmd}T00:00:00Z`);
  if (Number.isNaN(fromDate.getTime())) return false;
  const cutoff = new Date(nowUtc.getTime() - retentionDays * MS_PER_DAY);
  return fromDate < cutoff;
}

// Вычисляет absolute cutoff-дату для banner-описания. null = retention неизвестен
// (network error / loading), banner не render'ится.
export function retentionCutoffDate(
  retentionDays: number | null | undefined,
  nowUtc: Date
): Date | null {
  if (retentionDays == null || retentionDays <= 0) return null;
  return new Date(nowUtc.getTime() - retentionDays * MS_PER_DAY);
}
