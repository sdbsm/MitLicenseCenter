import { addMonths, format, parseISO } from "date-fns";
import { ru } from "date-fns/locale";
import type { ReportKind, ReportsFilters, ReportsRange } from "./types";

// MLC-185f: какой отчёт открыт — использование лицензий (дефолт) или размер баз.
// Хранится в URL (`?report=size`), чтобы выбор пережил перезагрузку. Любое иное/битое
// значение трактуется как дефолт «license» — страница не падает.
export function parseReportKind(params: URLSearchParams): ReportKind {
  return params.get("report") === "size" ? "size" : "license";
}

// Разбор URL → фильтры. Битый параметр не роняет страницу: tenantId берётся как есть
// (валидируется наличием в списке клиентов на месте), даты — сырой `YYYY-MM-DD` из
// `<input type="date">`. Любая невалидная дата просто не пройдёт в backend-range.
export function parseFiltersFromUrl(params: URLSearchParams): ReportsFilters {
  return {
    from: params.get("from"),
    to: params.get("to"),
    tenantId: params.get("tenant"),
  };
}

// MLC-185f: report-kind пишется только для «size» (дефолт «license» опускаем — чистый
// URL для основного отчёта). Старые вызовы без report → license → параметр не ставится,
// поведение прежнее.
export function filtersToUrl(
  filters: ReportsFilters,
  report: ReportKind = "license"
): URLSearchParams {
  const params = new URLSearchParams();
  if (filters.from) params.set("from", filters.from);
  if (filters.to) params.set("to", filters.to);
  if (filters.tenantId) params.set("tenant", filters.tenantId);
  if (report === "size") params.set("report", "size");
  return params;
}

// `<input type="date">` хранит «YYYY-MM-DD»; backend ждёт ISO UTC с временем.
// MLC-177: «1 день» = 00:00–24:00 в ЛОКАЛЬНОМ поясе браузера оператора, а не в UTC.
// Строим инстант через нативный `new Date(year, monthIndex, day, …)` (локальная
// полночь/конец суток) и сериализуем в UTC через `.toISOString()` — смещение пояса
// учитывается автоматически. Идём именно через конструктор Date, а не ручную
// арифметику смещения: на переходах DST сутки бывают 23/25 ч, и арифметика «съедет».
function parseDateOnly(date: string): { year: number; monthIndex: number; day: number } {
  const [year, month, day] = date.split("-").map(Number);
  return { year, monthIndex: month - 1, day };
}

// Начало дня для `from` — локальная полночь. Экспортируется для переиспользования
// в дашборде (трендовые карточки «Обзора», MLC-186c) — те же локально-суточные
// границы, что и у «Отчётов».
export function dateOnlyToIsoStart(date: string): string {
  const { year, monthIndex, day } = parseDateOnly(date);
  return new Date(year, monthIndex, day, 0, 0, 0, 0).toISOString();
}
// Конец дня для `to` — последняя миллисекунда локальных суток.
export function dateOnlyToIsoEnd(date: string): string {
  const { year, monthIndex, day } = parseDateOnly(date);
  return new Date(year, monthIndex, day, 23, 59, 59, 999).toISOString();
}

// Период для query backend. Пустые границы остаются null — дефолт (7 дней) и кламп
// (31 день) считает сервер, эффективный диапазон возвращается в fromUtc/toUtc.
export function buildBackendRange(filters: ReportsFilters): ReportsRange {
  return {
    from: filters.from ? dateOnlyToIsoStart(filters.from) : null,
    to: filters.to ? dateOnlyToIsoEnd(filters.to) : null,
  };
}

// MLC-054: помесячный выбор — заполняет те же date-only `from`/`to` границами месяца
// «YYYY-MM». Целый месяц всегда < 31 дня, поэтому серверный кламп не срабатывает (плашки
// обрезки не будет). Новых URL-параметров нет — месяц выводится из `from`.
// MLC-177: последний день месяца считаем через локальный `new Date(year, month, 0)`
// (день 0 следующего месяца = последний день текущего) — date-only границы затем
// раскрываются теми же локальными хелперами, что и сутки, так что месяц не «съезжает».
export function monthToRange(ym: string): { from: string; to: string } {
  const [year, month] = ym.split("-").map(Number);
  const lastDay = new Date(year, month, 0).getDate();
  return {
    from: `${ym}-01`,
    to: `${ym}-${String(lastDay).padStart(2, "0")}`,
  };
}

// Сдвиг «YYYY-MM» на delta месяцев (корректно переходит границу года).
export function shiftMonth(ym: string, delta: number): string {
  return format(addMonths(parseISO(`${ym}-01`), delta), "yyyy-MM");
}

// MLC-177: подпись точки на оси X графика. Бакет приходит как UTC-инстант
// (`bucketStartUtc`), `new Date(...)` рисует его в ЛОКАЛЬНОМ поясе браузера — так
// ось согласована с локальными границами периода. Вынесено из inline-useMemo в
// LicenseUsageChart ради тестируемости; поведение/формат не меняются.
export function formatBucketAxisLabel(bucketStartUtc: string): string {
  return format(new Date(bucketStartUtc), "dd.MM HH:mm", { locale: ru });
}
