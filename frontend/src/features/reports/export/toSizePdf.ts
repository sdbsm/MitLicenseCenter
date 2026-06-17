// Печатный PDF-отчёта размера баз на фронте: jsPDF, `dynamic import` по клику. Картинка
// графика рисуется ПЕРЕИСПОЛЬЗОВАНИЕМ Chart.js (offscreen-canvas → PNG), второй граф-движок
// не тащим. Кириллица: стандартные шрифты jsPDF её не знают — встраиваем сабсет Roboto
// (Apache-2.0) через addFileToVFS/addFont (см. fonts/). Таблицу разреза рисуем вручную
// (без jspdf-autotable — оптион-зависимости jsPDF замоканы, см. jspdfOptionalStub).
import { format } from "date-fns";
import { ru } from "date-fns/locale";
import { formatBytes } from "@/lib/formatBytes";
import { sizeScopeLabel, type SizeExportData } from "./sizeExport";
import { buildSizeChartData, SIZE_CHART_OPTIONS } from "./sizeChartConfig";
import {
  ROBOTO_FONT_NAME,
  ROBOTO_REGULAR_BASE64,
  ROBOTO_VFS_FILENAME,
} from "./fonts/robotoCyrillic";
import type { DatabaseSizePoint } from "../types";

function fmtFull(iso: string): string {
  return format(new Date(iso), "dd.MM.yyyy HH:mm", { locale: ru });
}

/** Offscreen-рендер графика роста в PNG через Chart.js. Возвращает data-URL либо `null`,
 *  если 2D-контекст canvas недоступен (node/jsdom при тестах) — тогда PDF строится
 *  без картинки (заголовок/таблица на месте). */
async function renderChartPng(points: DatabaseSizePoint[]): Promise<string | null> {
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
    data: buildSizeChartData(points),
    options: { ...SIZE_CHART_OPTIONS, devicePixelRatio: 2 },
  });
  const url = canvas.toDataURL("image/png");
  chart.destroy();
  return url;
}

/** Сериализация отчёта размера в PDF: заголовок (разрез + период) → картинка графика
 *  роста → таблица разреза (разбивка по клиентам для сводки / базы клиента для
 *  детализации, размеры через formatBytes). Текст — встроенным кириллическим Roboto. */
export async function toSizePdf(data: SizeExportData): Promise<Blob> {
  const { jsPDF } = await import("jspdf");

  // compress: true — zlib-сжатие потоков (через fflate), иначе картинка графика кладётся
  // несжатым битмапом и PDF раздувается до мегабайтов.
  const doc = new jsPDF({ unit: "pt", format: "a4", compress: true });
  doc.addFileToVFS(ROBOTO_VFS_FILENAME, ROBOTO_REGULAR_BASE64);
  doc.addFont(ROBOTO_VFS_FILENAME, ROBOTO_FONT_NAME, "normal");
  doc.setFont(ROBOTO_FONT_NAME, "normal");

  const pageWidth = doc.internal.pageSize.getWidth();
  const pageHeight = doc.internal.pageSize.getHeight();
  const marginX = 40;
  const contentWidth = pageWidth - marginX * 2;
  let y = 48;

  doc.setFontSize(16);
  doc.text(`Размер баз — ${sizeScopeLabel(data.scope)}`, marginX, y);
  y += 20;

  doc.setFontSize(11);
  doc.setTextColor(100);
  doc.text(`Период: ${fmtFull(data.fromUtc)} — ${fmtFull(data.toUtc)}`, marginX, y);
  y += 18;
  doc.setTextColor(15);

  const png = await renderChartPng(data.points);
  if (png) {
    const imgHeight = contentWidth * 0.38;
    doc.addImage(png, "PNG", marginX, y, contentWidth, imgHeight);
    y += imgHeight + 16;
  }

  // Таблица разреза. Сводка → разбивка по клиентам; детализация → базы клиента.
  const isSummary = data.scope === "all";
  const headers = isSummary
    ? ["Клиент", "Итого", "Данные", "Журнал", "Баз"]
    : ["База", "Итого", "Данные", "Журнал"];
  const rows: string[][] = isSummary
    ? data.tenants.map((r) => [
        r.tenantName ?? "Без клиента",
        formatBytes(r.totalBytes),
        formatBytes(r.dataBytes),
        formatBytes(r.logBytes),
        String(r.databaseCount),
      ])
    : data.databases.map((db) => [
        db.databaseName,
        formatBytes(db.totalBytes),
        formatBytes(db.dataBytes),
        formatBytes(db.logBytes),
      ]);

  // Колоночная сетка: первая колонка (имя) широкая и выровнена влево, числовые —
  // фиксированной ширины справа.
  const numColWidth = 70;
  const numCount = headers.length - 1;
  const nameWidth = contentWidth - numColWidth * numCount;
  const colX: number[] = [marginX];
  for (let i = 0; i < numCount; i++) {
    colX.push(marginX + nameWidth + numColWidth * i);
  }
  const rowHeight = 16;

  const drawHeader = () => {
    doc.setFontSize(10);
    doc.setTextColor(100);
    doc.text(headers[0], colX[0], y);
    for (let i = 1; i < headers.length; i++) {
      doc.text(headers[i], colX[i] + numColWidth, y, { align: "right" });
    }
    y += 6;
    doc.setDrawColor(200);
    doc.line(marginX, y, marginX + contentWidth, y);
    y += rowHeight - 2;
    doc.setTextColor(15);
  };

  doc.setFontSize(11);
  drawHeader();
  doc.setFontSize(10);

  for (const row of rows) {
    if (y > pageHeight - marginX) {
      doc.addPage();
      y = 48;
      doc.setFontSize(11);
      drawHeader();
      doc.setFontSize(10);
    }
    // Имя усекаем под ширину колонки, чтобы не наезжало на числа.
    const name = doc.splitTextToSize(row[0], nameWidth - 8)[0] ?? row[0];
    doc.text(name, colX[0], y);
    for (let i = 1; i < row.length; i++) {
      doc.text(row[i], colX[i] + numColWidth, y, { align: "right" });
    }
    y += rowHeight;
  }

  return doc.output("blob");
}
