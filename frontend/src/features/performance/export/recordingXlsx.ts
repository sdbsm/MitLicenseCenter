import { format } from "date-fns";
import { ru } from "date-fns/locale";
import type { RecordingSample, RecordingSummary } from "../types";

function fmt(iso: string): string {
  return format(new Date(iso), "dd.MM.yyyy HH:mm:ss", { locale: ru });
}

function round1(value: number): number {
  return Math.round(value * 10) / 10;
}

const STATUS_LABEL: Record<RecordingSummary["status"], string> = {
  Active: "Идёт",
  Stopped: "Остановлена",
  Interrupted: "Прервана",
};

/** Сериализация записи в книгу Excel (SheetJS) — `dynamic import` по клику, чтобы тяжёлая либа
 *  не попадала в основной бандл (как `features/reports/export/toXlsx`). Лист «Сводка» (метаданные
 *  расследования) + лист «Host-метрики» (ряд сэмплов настоящими числами — для графиков/сводных). */
export async function recordingToXlsx(
  recording: RecordingSummary,
  samples: readonly RecordingSample[]
): Promise<Blob> {
  const XLSX = await import("xlsx");

  const summaryRows: (string | number)[][] = [
    ["Начало", fmt(recording.startedAtUtc)],
    ["Окончание", recording.stoppedAtUtc ? fmt(recording.stoppedAtUtc) : "—"],
    ["Статус", STATUS_LABEL[recording.status]],
    ["Кто запустил", recording.startedBy],
    ["Сэмплов", recording.sampleCount],
  ];
  const summarySheet = XLSX.utils.aoa_to_sheet(summaryRows);

  const dataRows: (string | number)[][] = [
    [
      "Время",
      "ЦП %",
      "Очередь ЦП",
      "Память свободно МБ",
      "Память всего МБ",
      "Обмен стр/с",
      "Диск чтение мс",
      "Диск запись мс",
      "Очередь диска",
      "Недоступно процессов",
    ],
    ...samples.map((s) => [
      fmt(s.sampleUtc),
      round1(s.cpuPercent),
      round1(s.cpuQueueLength),
      round1(s.memoryAvailableMBytes),
      round1(s.memoryTotalMBytes),
      round1(s.memoryPagesPerSec),
      round1(s.diskAvgReadSecPerOp * 1000),
      round1(s.diskAvgWriteSecPerOp * 1000),
      round1(s.diskQueueLength),
      s.processesInaccessible,
    ]),
  ];
  const dataSheet = XLSX.utils.aoa_to_sheet(dataRows);

  const wb = XLSX.utils.book_new();
  XLSX.utils.book_append_sheet(wb, summarySheet, "Сводка");
  XLSX.utils.book_append_sheet(wb, dataSheet, "Host-метрики");

  const buffer = XLSX.write(wb, { type: "array", bookType: "xlsx" }) as ArrayBuffer;
  return new Blob([buffer], {
    type: "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
  });
}
