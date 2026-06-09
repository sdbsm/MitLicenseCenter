import { format } from "date-fns";
import { ru } from "date-fns/locale";
import type { RecordingSample } from "../types";

/** UTF-8 BOM (U+FEFF) — чтобы RU-Excel распознал кодировку. Литерал U+FEFF в исходнике
 *  тулинг сборки вырезает, поэтому пишем escape'ом (как `features/reports/export/toCsv`). */
const BOM = String.fromCharCode(0xfeff);

/** Число с десятичной запятой для RU-Excel, округление до десятых. */
function num(value: number): string {
  return (Math.round(value * 10) / 10).toString().replace(".", ",");
}

/** Ряд сэмплов записи в CSV (host-метрики во времени), дружелюбный к RU-Excel: UTF-8 с BOM,
 *  разделитель «;», десятичная запятая. Латентность диска переведена в мс. Топ-виновники 1С/SQL
 *  в CSV не кладём — это плоская host-таблица (как сводка в reports CSV). */
export function recordingToCsv(samples: readonly RecordingSample[]): Blob {
  const header =
    "Время;ЦП %;Очередь ЦП;Память свободно МБ;Память всего МБ;Обмен стр/с;" +
    "Диск чтение мс;Диск запись мс;Очередь диска;Недоступно процессов";
  const rows = samples.map((s) => {
    const when = format(new Date(s.sampleUtc), "dd.MM.yyyy HH:mm:ss", { locale: ru });
    return [
      when,
      num(s.cpuPercent),
      num(s.cpuQueueLength),
      num(s.memoryAvailableMBytes),
      num(s.memoryTotalMBytes),
      num(s.memoryPagesPerSec),
      num(s.diskAvgReadSecPerOp * 1000),
      num(s.diskAvgWriteSecPerOp * 1000),
      num(s.diskQueueLength),
      s.processesInaccessible,
    ].join(";");
  });
  const csv = BOM + [header, ...rows].join("\r\n") + "\r\n";
  return new Blob([csv], { type: "text/csv;charset=utf-8" });
}
