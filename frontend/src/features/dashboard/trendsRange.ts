import { format, subDays } from "date-fns";
import { dateOnlyToIsoStart, dateOnlyToIsoEnd } from "@/features/reports/reportsUrlState";
import type { ReportsRange } from "@/features/reports/types";

/**
 * Фиксированный «последние N дней» диапазон для трендовых карточек «Обзора» (MLC-186c).
 *
 * Границы строятся теми же локально-суточными хелперами, что и «Отчёты»
 * (`dateOnlyToIsoStart`/`dateOnlyToIsoEnd`, MLC-177): `from` — локальная полночь
 * N дней назад, `to` — конец сегодняшних локальных суток. Так тренд на дашборде
 * согласован с тем, что покажет полноценный отчёт за тот же период.
 *
 * ВАЖНО (стабильность react-query): результат пересчитывается на каждый вызов
 * (`new Date()`), поэтому в DashboardPage диапазон фиксируется один раз через
 * `useMemo(() => lastNDaysRange(7), [])` — иначе новый объект каждый рендер даёт
 * новый query-key и бесконечный рефетч.
 */
export function lastNDaysRange(n: number): ReportsRange {
  const today = new Date();
  return {
    from: dateOnlyToIsoStart(format(subDays(today, n), "yyyy-MM-dd")),
    to: dateOnlyToIsoEnd(format(today, "yyyy-MM-dd")),
  };
}
