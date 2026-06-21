// PDF-экспорт отчёта расследования (MLC-244, трек 1.2).
// ТЕКСТОВЫЙ PDF (без графиков) — переиспользует паттерн reports/export/toPdf.ts:
// динамический import jsPDF, кириллический Roboto через addFileToVFS/addFont.
// Структура: шапка → резюме → что собрано → находки с рекомендациями → подвал.
import { format } from "date-fns";
import { ru } from "date-fns/locale";
import type { jsPDF } from "jspdf";
import {
  ROBOTO_FONT_NAME,
  ROBOTO_REGULAR_BASE64,
  ROBOTO_VFS_FILENAME,
} from "@/features/reports/export/fonts/robotoCyrillic";
import type { CollectionConfig, InvestigationReport } from "./types";

function fmtDate(iso: string): string {
  return format(new Date(iso), "dd.MM.yyyy HH:mm", { locale: ru });
}

function fmtMicros(micros: number): string {
  if (micros >= 1_000_000) return `${(micros / 1_000_000).toFixed(1)} с`;
  if (micros >= 1_000) return `${(micros / 1_000).toFixed(0)} мс`;
  return `${micros} мкс`;
}

const SEVERITY_LABEL: Record<string, string> = {
  None: "Нет находок",
  Info: "К сведению",
  Warning: "Внимание",
};

const SCENARIO_LABEL: Record<string, string> = {
  Locks: "Управляемые блокировки 1С",
  SlowQueries: "Долгие запросы к СУБД",
  Exceptions: "Исключения платформы",
  GeneralSlow: "Общая медленная работа",
  DbmsLocks: "Блокировки уровня СУБД",
};

/** Выводит текст и продвигает y; при необходимости добавляет страницу. */
function addLine(
  doc: jsPDF,
  text: string,
  x: number,
  y: number,
  maxWidth: number,
  lineHeight: number,
  pageHeight: number,
  marginBottom: number
): number {
  const lines = doc.splitTextToSize(text, maxWidth);
  for (const line of lines) {
    if (y + lineHeight > pageHeight - marginBottom) {
      doc.addPage();
      y = 48;
    }
    doc.text(line, x, y);
    y += lineHeight;
  }
  return y;
}

/**
 * Формирует текстовый PDF отчёта расследования.
 * Переиспользует инфраструктуру: jsPDF + Roboto-кириллица из reports/export/fonts/.
 */
export async function toInvestigationPdf(
  report: InvestigationReport,
  collectionConfig: CollectionConfig | null,
  infobaseName: string | null
): Promise<Blob> {
  const { jsPDF } = await import("jspdf");

  const doc = new jsPDF({ unit: "pt", format: "a4", compress: true });
  doc.addFileToVFS(ROBOTO_VFS_FILENAME, ROBOTO_REGULAR_BASE64);
  doc.addFont(ROBOTO_VFS_FILENAME, ROBOTO_FONT_NAME, "normal");
  doc.setFont(ROBOTO_FONT_NAME, "normal");

  const pageWidth = doc.internal.pageSize.getWidth();
  const pageHeight = doc.internal.pageSize.getHeight();
  const marginX = 40;
  const marginBottom = 48;
  const contentWidth = pageWidth - marginX * 2;
  const lh = 16; // line height
  let y = 48;

  const shortId = report.summary.id.slice(0, 8);
  const scopeLabel = infobaseName ?? "Весь узел";
  const scenarioLabel = SCENARIO_LABEL[report.summary.scenario] ?? report.summary.scenario;

  // ── Заголовок ──────────────────────────────────────────────────────────────
  doc.setFontSize(18);
  doc.setTextColor(15);
  y = addLine(
    doc,
    `Отчёт по расследованию №${shortId}`,
    marginX,
    y,
    contentWidth,
    22,
    pageHeight,
    marginBottom
  );
  y += 4;

  doc.setFontSize(10);
  doc.setTextColor(100);
  y = addLine(
    doc,
    `Сформирован: ${fmtDate(report.generatedAtUtc)}`,
    marginX,
    y,
    contentWidth,
    lh,
    pageHeight,
    marginBottom
  );

  const period =
    fmtDate(report.summary.startedAtUtc) +
    (report.summary.stoppedAtUtc ? ` – ${fmtDate(report.summary.stoppedAtUtc)}` : "");
  y = addLine(doc, `Период: ${period}`, marginX, y, contentWidth, lh, pageHeight, marginBottom);
  y = addLine(doc, `Узел: текущий узел`, marginX, y, contentWidth, lh, pageHeight, marginBottom);
  y = addLine(
    doc,
    `Сценарий: ${scenarioLabel}`,
    marginX,
    y,
    contentWidth,
    lh,
    pageHeight,
    marginBottom
  );
  y = addLine(doc, `Цель: ${scopeLabel}`, marginX, y, contentWidth, lh, pageHeight, marginBottom);
  y = addLine(
    doc,
    `Запустил: ${report.summary.startedBy}`,
    marginX,
    y,
    contentWidth,
    lh,
    pageHeight,
    marginBottom
  );
  y += 12;

  // Разделитель
  doc.setDrawColor(200);
  doc.line(marginX, y, pageWidth - marginX, y);
  y += 16;

  // ── Резюме ─────────────────────────────────────────────────────────────────
  doc.setFontSize(13);
  doc.setTextColor(15);
  y = addLine(doc, "Резюме", marginX, y, contentWidth, 18, pageHeight, marginBottom);
  y += 4;
  doc.setFontSize(10);

  if (report.items.length === 0) {
    doc.setTextColor(100);
    y = addLine(
      doc,
      "Существенных проблем не выявлено.",
      marginX,
      y,
      contentWidth,
      lh,
      pageHeight,
      marginBottom
    );
  } else {
    for (const item of report.items) {
      const severity = SEVERITY_LABEL[item.severity] ?? item.severity;
      doc.setTextColor(15);
      y = addLine(
        doc,
        `[${severity}] ${item.headline}`,
        marginX,
        y,
        contentWidth,
        lh,
        pageHeight,
        marginBottom
      );
      doc.setTextColor(80);
      y = addLine(
        doc,
        item.recommendation,
        marginX + 12,
        y,
        contentWidth - 12,
        lh,
        pageHeight,
        marginBottom
      );
      y += 4;
    }
  }
  y += 8;

  // ── Что собрано ────────────────────────────────────────────────────────────
  doc.line(marginX, y, pageWidth - marginX, y);
  y += 16;
  doc.setFontSize(13);
  doc.setTextColor(15);
  y = addLine(doc, "Что собрано", marginX, y, contentWidth, 18, pageHeight, marginBottom);
  y += 4;
  doc.setFontSize(10);

  if (collectionConfig) {
    doc.setTextColor(15);
    y = addLine(
      doc,
      `События ТЖ: ${collectionConfig.events}`,
      marginX,
      y,
      contentWidth,
      lh,
      pageHeight,
      marginBottom
    );
    y = addLine(
      doc,
      `Формат: ${collectionConfig.format}`,
      marginX,
      y,
      contentWidth,
      lh,
      pageHeight,
      marginBottom
    );
    y = addLine(
      doc,
      `Окно истории: ${collectionConfig.historyHours} ч`,
      marginX,
      y,
      contentWidth,
      lh,
      pageHeight,
      marginBottom
    );
    if (collectionConfig.durationThresholdMicros != null) {
      y = addLine(
        doc,
        `Порог длительности: ${fmtMicros(collectionConfig.durationThresholdMicros)}`,
        marginX,
        y,
        contentWidth,
        lh,
        pageHeight,
        marginBottom
      );
    }
    if (collectionConfig.processNameFilter) {
      y = addLine(
        doc,
        `Фильтр процесса (изоляция ИБ): ${collectionConfig.processNameFilter}`,
        marginX,
        y,
        contentWidth,
        lh,
        pageHeight,
        marginBottom
      );
    }
    y += 4;
    doc.setTextColor(100);
    y = addLine(
      doc,
      "Сырьё технологического журнала удалено после разбора.",
      marginX,
      y,
      contentWidth,
      lh,
      pageHeight,
      marginBottom
    );
  } else {
    doc.setTextColor(100);
    y = addLine(
      doc,
      "Данные о конфигурации сбора недоступны (историческое дело).",
      marginX,
      y,
      contentWidth,
      lh,
      pageHeight,
      marginBottom
    );
  }
  y += 8;

  // ── Находки ────────────────────────────────────────────────────────────────
  doc.line(marginX, y, pageWidth - marginX, y);
  y += 16;
  doc.setFontSize(13);
  doc.setTextColor(15);
  y = addLine(doc, "Находки", marginX, y, contentWidth, 18, pageHeight, marginBottom);
  y += 4;
  doc.setFontSize(10);

  if (report.items.length === 0) {
    doc.setTextColor(100);
    y = addLine(doc, "Находок нет.", marginX, y, contentWidth, lh, pageHeight, marginBottom);
  } else {
    for (let i = 0; i < report.items.length; i++) {
      const item = report.items[i];
      const severity = SEVERITY_LABEL[item.severity] ?? item.severity;

      doc.setTextColor(15);
      y = addLine(
        doc,
        `${i + 1}. [${severity}] ${item.headline} (событий: ${item.count})`,
        marginX,
        y,
        contentWidth,
        lh,
        pageHeight,
        marginBottom
      );
      doc.setTextColor(60);
      y = addLine(
        doc,
        `Рекомендация: ${item.recommendation}`,
        marginX + 12,
        y,
        contentWidth - 12,
        lh,
        pageHeight,
        marginBottom
      );
      y += 6;
    }
  }
  y += 8;

  // ── Подвал ─────────────────────────────────────────────────────────────────
  doc.line(marginX, y, pageWidth - marginX, y);
  y += 14;
  doc.setFontSize(9);
  doc.setTextColor(120);
  addLine(
    doc,
    "Сырьё удалено по retention; дело доступно post-mortem. Источники методик — ИТС (its.1c.ru).",
    marginX,
    y,
    contentWidth,
    14,
    pageHeight,
    marginBottom
  );

  return doc.output("blob");
}
