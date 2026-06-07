import { format } from "date-fns";
import type { LicenseUsageSeriesResponse } from "../types";

/** Разрез экспорта: сводка по всем клиентам или детализация по одному клиенту. */
export type ExportScope = "all" | { tenantName: string | null };

/** Файловый slug имени клиента: нижний регистр, пробелы → «-», вырезаны
 *  filesystem-небезопасные символы. Кириллица сохраняется (читаемость для RU,
 *  на Windows допустима в имени файла). Пустое имя → «client». */
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

/** Имя файла выгрузки: `license-usage_<scope>_<from>_<to>.<ext>`, где scope = «all»
 *  (сводка) или slug имени клиента (детализация). Диапазон — эффективный
 *  `fromUtc`/`toUtc` ответа, date-only. */
export function exportFilename(
  scope: ExportScope,
  data: LicenseUsageSeriesResponse,
  ext: string
): string {
  const scopePart = scope === "all" ? "all" : slugifyTenant(scope.tenantName);
  const from = format(new Date(data.fromUtc), "yyyy-MM-dd");
  const to = format(new Date(data.toUtc), "yyyy-MM-dd");
  return `license-usage_${scopePart}_${from}_${to}.${ext}`;
}
