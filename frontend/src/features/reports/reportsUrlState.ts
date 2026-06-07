import { addMonths, endOfMonth, format, parseISO } from "date-fns";
import type { ReportsFilters, ReportsRange } from "./types";

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

export function filtersToUrl(filters: ReportsFilters): URLSearchParams {
  const params = new URLSearchParams();
  if (filters.from) params.set("from", filters.from);
  if (filters.to) params.set("to", filters.to);
  if (filters.tenantId) params.set("tenant", filters.tenantId);
  return params;
}

// `<input type="date">` хранит «YYYY-MM-DD»; backend ждёт ISO UTC с временем.
// Начало дня для `from`, конец — для `to` (как в auditUrlState).
function dateOnlyToIsoStart(date: string): string {
  return `${date}T00:00:00Z`;
}
function dateOnlyToIsoEnd(date: string): string {
  return `${date}T23:59:59Z`;
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
export function monthToRange(ym: string): { from: string; to: string } {
  return {
    from: `${ym}-01`,
    to: format(endOfMonth(parseISO(`${ym}-01`)), "yyyy-MM-dd"),
  };
}

// Сдвиг «YYYY-MM» на delta месяцев (корректно переходит границу года).
export function shiftMonth(ym: string, delta: number): string {
  return format(addMonths(parseISO(`${ym}-01`), delta), "yyyy-MM");
}
