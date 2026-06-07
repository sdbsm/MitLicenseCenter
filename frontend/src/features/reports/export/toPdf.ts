// Печатный PDF-отчёт на фронте (MLC-052): jsPDF + jsPDF-AutoTable, `dynamic import`
// по клику. Картинка графика рисуется ПЕРЕИСПОЛЬЗОВАНИЕМ Chart.js (offscreen-canvas →
// PNG), второй граф-движок не тащим. Кириллица: стандартные шрифты jsPDF её не знают —
// встраиваем сабсет Roboto (Apache-2.0) через addFileToVFS/addFont (см. fonts/).
import { format } from "date-fns";
import { ru } from "date-fns/locale";
import type { LicenseUsageSeriesResponse } from "../types";
import { buildChartData, CHART_OPTIONS } from "./chartConfig";
import type { ExportScope } from "./exportFilename";
import {
  ROBOTO_FONT_NAME,
  ROBOTO_REGULAR_BASE64,
  ROBOTO_VFS_FILENAME,
} from "./fonts/robotoCyrillic";

function fmtFull(iso: string): string {
  return format(new Date(iso), "dd.MM.yyyy HH:mm", { locale: ru });
}

function round1(value: number): number {
  return Math.round(value * 10) / 10;
}

/** Offscreen-рендер графика в PNG через Chart.js. Возвращает data-URL либо `null`,
 *  если 2D-контекст canvas недоступен (node/jsdom при тестах) — тогда PDF строится
 *  без картинки (заголовок/сводка/таблица на месте). */
async function renderChartPng(data: LicenseUsageSeriesResponse): Promise<string | null> {
  if (typeof document === "undefined") {
    return null;
  }
  const canvas = document.createElement("canvas");
  canvas.width = 1000;
  canvas.height = 380;
  const ctx = canvas.getContext("2d");
  if (!ctx) {
    return null;
  }
  const { Chart, registerables } = await import("chart.js");
  Chart.register(...registerables);
  const chart = new Chart(ctx, {
    type: "line",
    data: buildChartData(data),
    options: { ...CHART_OPTIONS, devicePixelRatio: 2 },
  });
  const url = canvas.toDataURL("image/png");
  chart.destroy();
  return url;
}

/** Сериализация ряда в PDF: заголовок (разрез + период) → сводка (пик/среднее) →
 *  картинка графика → таблица бакетов. Текст — встроенным кириллическим Roboto. */
export async function toPdf(data: LicenseUsageSeriesResponse, scope: ExportScope): Promise<Blob> {
  const { jsPDF } = await import("jspdf");
  const { default: autoTable } = await import("jspdf-autotable");

  // compress: true — zlib-сжатие потоков (через fflate), иначе картинка графика кладётся
  // несжатым битмапом и PDF раздувается до мегабайтов.
  const doc = new jsPDF({ unit: "pt", format: "a4", compress: true });
  doc.addFileToVFS(ROBOTO_VFS_FILENAME, ROBOTO_REGULAR_BASE64);
  doc.addFont(ROBOTO_VFS_FILENAME, ROBOTO_FONT_NAME, "normal");
  doc.setFont(ROBOTO_FONT_NAME, "normal");

  const pageWidth = doc.internal.pageSize.getWidth();
  const marginX = 40;
  const contentWidth = pageWidth - marginX * 2;
  let y = 48;

  const scopeLabel = scope === "all" ? "Все клиенты" : (scope.tenantName ?? "Клиент");
  const percent = data.peakLimit > 0 ? Math.round((data.peakConsumed / data.peakLimit) * 100) : 0;
  const peakAt = data.peakAtUtc ? ` (${fmtFull(data.peakAtUtc)})` : "";

  doc.setFontSize(16);
  doc.text(`Использование лицензий — ${scopeLabel}`, marginX, y);
  y += 20;

  doc.setFontSize(11);
  doc.setTextColor(100);
  doc.text(`Период: ${fmtFull(data.fromUtc)} — ${fmtFull(data.toUtc)}`, marginX, y);
  y += 18;
  doc.setTextColor(15);
  doc.text(
    `Пик за период: ${data.peakConsumed} из ${data.peakLimit} (${percent}%)${peakAt}`,
    marginX,
    y
  );
  y += 16;
  doc.text(`Среднее за период: ${round1(data.averageConsumed)}`, marginX, y);
  y += 16;

  const png = await renderChartPng(data);
  if (png) {
    const imgHeight = contentWidth * 0.38;
    doc.addImage(png, "PNG", marginX, y, contentWidth, imgHeight);
    y += imgHeight + 12;
  }

  autoTable(doc, {
    startY: y,
    margin: { left: marginX, right: marginX },
    head: [["Начало бакета", "Среднее", "Пик", "Лимит"]],
    body: data.buckets.map((b) => [
      fmtFull(b.bucketStartUtc),
      String(round1(b.consumedAvg)),
      String(b.consumedMax),
      String(b.limit),
    ]),
    // Только стиль «normal» зарегистрирован (сабсет одного начертания) — фиксируем
    // его и для шапки, иначе AutoTable возьмёт helvetica-bold (без кириллицы).
    styles: { font: ROBOTO_FONT_NAME, fontStyle: "normal", fontSize: 9 },
    headStyles: {
      font: ROBOTO_FONT_NAME,
      fontStyle: "normal",
      fillColor: [241, 245, 249],
      textColor: 15,
    },
    columnStyles: { 1: { halign: "right" }, 2: { halign: "right" }, 3: { halign: "right" } },
  });

  return doc.output("blob");
}
