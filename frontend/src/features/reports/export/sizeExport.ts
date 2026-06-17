import { format } from "date-fns";
import type { DatabaseSizeDatabaseRow, DatabaseSizePoint, DatabaseSizeTenantRow } from "../types";

/** Разрез экспорта размера баз:
 *  • сводка по хосту (`"all"`) — таблица разбивки по клиентам;
 *  • детализация по клиенту (`{ tenantName }`) — таблица баз клиента.
 *  Зеркалит ExportScope лицензий, но несёт имя клиента для детализации (там нет
 *  «без клиента» — drill-down всегда по выбранному тенанту). */
export type SizeExportScope = "all" | { tenantName: string | null };

/** Нормализованный вход size-сериалайзеров: ряд роста во времени + одна табличная
 *  секция (клиенты для сводки ИЛИ базы для детализации) + эффективный период. Сводим
 *  оба ответа (DatabaseSizeSeriesResponse / DatabaseSizeTenantSeriesResponse) к одной
 *  форме, чтобы сериалайзеры не ветвились по типу ответа. */
export interface SizeExportData {
  scope: SizeExportScope;
  points: DatabaseSizePoint[];
  fromUtc: string;
  toUtc: string;
  // Ровно одна из таблиц непуста по построению разреза; обе массивы для единообразия.
  tenants: DatabaseSizeTenantRow[];
  databases: DatabaseSizeDatabaseRow[];
}

/** Файловый slug имени клиента — та же конвенция, что у лицензий (нижний регистр,
 *  пробелы → «-», вырезаны filesystem-небезопасные символы, кириллица сохранена).
 *  Пустое имя → «client». */
function slugifyTenant(name: string | null): string {
  const slug = (name ?? "")
    .trim()
    .toLowerCase()
    .replace(/[\\/:*?"<>|]/g, "")
    .replace(/\s+/g, "-")
    .replace(/-+/g, "-")
    .replace(/^-|-$/g, "");
  return slug || "client";
}

/** Имя файла выгрузки размера: `database-size_<scope>_<from>_<to>.<ext>`, где scope =
 *  «all» (сводка) или slug имени клиента (детализация). Диапазон — эффективный
 *  `fromUtc`/`toUtc` ответа, date-only. Префикс «database-size» отличает выгрузку от
 *  лицензионной «license-usage». */
export function sizeExportFilename(data: SizeExportData, ext: string): string {
  const scopePart = data.scope === "all" ? "all" : slugifyTenant(data.scope.tenantName);
  const from = format(new Date(data.fromUtc), "yyyy-MM-dd");
  const to = format(new Date(data.toUtc), "yyyy-MM-dd");
  return `database-size_${scopePart}_${from}_${to}.${ext}`;
}

/** Подпись разреза для заголовков/листов выгрузки. */
export function sizeScopeLabel(scope: SizeExportScope): string {
  return scope === "all" ? "Все клиенты" : (scope.tenantName ?? "Клиент");
}
