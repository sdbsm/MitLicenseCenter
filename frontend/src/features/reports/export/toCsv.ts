import { format } from "date-fns";
import { ru } from "date-fns/locale";
import type { LicenseUsageSeriesResponse } from "../types";

/** UTF-8 BOM (U+FEFF) — чтобы RU-Excel распознал кодировку и не показал кракозябры.
 *  Записываем escape'ом: литерал U+FEFF в исходнике вырезается тулингом сборки. */
const BOM = String.fromCharCode(0xfeff);

/** Десятичная запятая для RU-Excel: округляем среднее до десятых (как график,
 *  `LicenseUsageChart.tsx`) и меняем точку на запятую. */
function avgCell(value: number): string {
  return (Math.round(value * 10) / 10).toString().replace(".", ",");
}

/** Сериализация бакетов в CSV, дружелюбный к RU-Excel (в духе гоч проекта):
 *  UTF-8 с BOM, разделитель «;», десятичная запятая. Чистая таблица — пик/среднее
 *  сводки в CSV не кладём (они уходят в богатые форматы XLSX). */
export function toCsv(data: LicenseUsageSeriesResponse): Blob {
  const header = "Начало бакета;Среднее;Пик;Лимит";
  const rows = data.buckets.map((b) => {
    const when = format(new Date(b.bucketStartUtc), "dd.MM.yyyy HH:mm", { locale: ru });
    return `${when};${avgCell(b.consumedAvg)};${b.consumedMax};${b.limit}`;
  });
  const csv = BOM + [header, ...rows].join("\r\n") + "\r\n";
  return new Blob([csv], { type: "text/csv;charset=utf-8" });
}
