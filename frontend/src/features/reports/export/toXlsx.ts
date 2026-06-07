import { format } from "date-fns";
import { ru } from "date-fns/locale";
import type { LicenseUsageSeriesResponse } from "../types";
import type { ExportScope } from "./exportFilename";

function fmt(iso: string): string {
  return format(new Date(iso), "dd.MM.yyyy HH:mm", { locale: ru });
}

function round1(value: number): number {
  return Math.round(value * 10) / 10;
}

/** Сериализация ряда в книгу Excel (SheetJS) — `dynamic import` по клику, чтобы
 *  тяжёлая либа не попадала в основной бандл. Два листа: «Сводка» (метаданные
 *  разреза) и «Данные» (таблица бакетов). Числа кладутся НАСТОЯЩИМИ числами,
 *  чтобы в Excel работали сводные/графики. */
export async function toXlsx(data: LicenseUsageSeriesResponse, scope: ExportScope): Promise<Blob> {
  const XLSX = await import("xlsx");

  const scopeLabel = scope === "all" ? "Все клиенты" : (scope.tenantName ?? "Клиент");

  const summaryRows: (string | number)[][] = [
    ["Разрез", scopeLabel],
    ["Период с", fmt(data.fromUtc)],
    ["Период по", fmt(data.toUtc)],
    ["Пик потребления", data.peakConsumed],
    ["Лимит на момент пика", data.peakLimit],
    ["Момент пика", data.peakAtUtc ? fmt(data.peakAtUtc) : "—"],
    ["Среднее потребление", round1(data.averageConsumed)],
  ];
  const summarySheet = XLSX.utils.aoa_to_sheet(summaryRows);

  const dataRows: (string | number)[][] = [
    ["Начало бакета", "Среднее", "Пик", "Лимит"],
    ...data.buckets.map((b) => [
      fmt(b.bucketStartUtc),
      round1(b.consumedAvg),
      b.consumedMax,
      b.limit,
    ]),
  ];
  const dataSheet = XLSX.utils.aoa_to_sheet(dataRows);

  const wb = XLSX.utils.book_new();
  XLSX.utils.book_append_sheet(wb, summarySheet, "Сводка");
  XLSX.utils.book_append_sheet(wb, dataSheet, "Данные");

  const buffer = XLSX.write(wb, { type: "array", bookType: "xlsx" }) as ArrayBuffer;
  return new Blob([buffer], {
    type: "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
  });
}
