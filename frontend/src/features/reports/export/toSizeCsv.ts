import { format } from "date-fns";
import { ru } from "date-fns/locale";
import type { SizeExportData } from "./sizeExport";

/** UTF-8 BOM (U+FEFF) — чтобы RU-Excel распознал кодировку и не показал кракозябры.
 *  Записываем escape'ом: литерал U+FEFF в исходнике вырезается тулингом сборки. */
const BOM = String.fromCharCode(0xfeff);

function fmt(iso: string): string {
  return format(new Date(iso), "dd.MM.yyyy HH:mm", { locale: ru });
}

/** Экранирование CSV-ячейки: имя клиента/базы может содержать «;» или кавычки —
 *  оборачиваем в кавычки и удваиваем внутренние (RFC 4180). */
function cell(value: string): string {
  return /[;"\r\n]/.test(value) ? `"${value.replace(/"/g, '""')}"` : value;
}

/** Сериализация отчёта размера в CSV, дружелюбный к RU-Excel (гочи проекта): UTF-8 с BOM,
 *  разделитель «;». Размеры кладём СЫРЫМИ байтами (как лицензионный CSV кладёт сырые
 *  числа) — машинно-обрабатываемо, перевод в КБ/МБ/ГБ остаётся за получателем. Две секции
 *  через пустую строку: «Рост размера» (ряд во времени) и таблица разреза («Клиенты» для
 *  сводки / «Базы» для детализации). */
export function toSizeCsv(data: SizeExportData): Blob {
  const lines: string[] = [];

  // Секция 1 — ряд роста итога во времени (байты).
  lines.push("Момент;Данные (байт);Журнал (байт);Итого (байт)");
  for (const p of data.points) {
    lines.push(`${fmt(p.atUtc)};${p.dataBytes};${p.logBytes};${p.totalBytes}`);
  }

  // Секция 2 — таблица разреза. Сводка → разбивка по клиентам (с числом баз);
  // детализация → базы клиента.
  if (data.scope === "all") {
    lines.push("");
    lines.push("Клиент;Итого (байт);Данные (байт);Журнал (байт);Число баз");
    for (const row of data.tenants) {
      const name = row.tenantName ?? "Без клиента";
      lines.push(
        `${cell(name)};${row.totalBytes};${row.dataBytes};${row.logBytes};${row.databaseCount}`
      );
    }
  } else {
    lines.push("");
    lines.push("База;Итого (байт);Данные (байт);Журнал (байт)");
    for (const db of data.databases) {
      lines.push(`${cell(db.databaseName)};${db.totalBytes};${db.dataBytes};${db.logBytes}`);
    }
  }

  const csv = BOM + lines.join("\r\n") + "\r\n";
  return new Blob([csv], { type: "text/csv;charset=utf-8" });
}
