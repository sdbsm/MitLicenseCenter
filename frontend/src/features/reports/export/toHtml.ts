// Самодостаточный офлайн-HTML с тем же интерактивным графиком, что в панели (MLC-052).
// Исходник Chart.js ИНЛАЙНИТСЯ в файл (без CDN) — UMD-сборка авто-регистрирует
// контроллеры, поэтому в файле достаточно `new Chart(...)`. Исходник отдаёт виртуальный
// модуль (см. плагин chartjsUmdSource в vite.config; тип — src/vite-env.d.ts).
import chartSrc from "virtual:chartjs-umd-src";
import { format } from "date-fns";
import { ru } from "date-fns/locale";
import type { LicenseUsageSeriesResponse } from "../types";
import { buildChartData, CHART_OPTIONS } from "./chartConfig";
import type { ExportScope } from "./exportFilename";

function fmtFull(iso: string): string {
  return format(new Date(iso), "dd.MM.yyyy HH:mm", { locale: ru });
}

function round1(value: number): number {
  return Math.round(value * 10) / 10;
}

function escapeHtml(text: string): string {
  return text
    .replace(/&/g, "&amp;")
    .replace(/</g, "&lt;")
    .replace(/>/g, "&gt;")
    .replace(/"/g, "&quot;");
}

/** Безопасная вставка JSON в `<script>`: экранируем `<` (и закрывающий тег),
 *  чтобы данные (в т.ч. имя клиента) не разорвали разметку. */
function jsonForScript(value: unknown): string {
  return JSON.stringify(value).replace(/</g, "\\u003c");
}

/** Сериализация ряда в самодостаточный HTML-документ: сводка (текст) + интерактивный
 *  график Chart.js (инлайн-движок). Открывается офлайн в любом браузере; график оживает
 *  при открытии (canvas на этапе формирования не нужен). Сырую побакетную таблицу
 *  презентационная выгрузка не несёт (MLC-054) — она шумна на длинных периодах. */
export function toHtml(data: LicenseUsageSeriesResponse, scope: ExportScope): Blob {
  const scopeLabel = scope === "all" ? "Все клиенты" : (scope.tenantName ?? "Клиент");
  const percent = data.peakLimit > 0 ? Math.round((data.peakConsumed / data.peakLimit) * 100) : 0;
  const peakAt = data.peakAtUtc ? fmtFull(data.peakAtUtc) : null;

  // Оговорка про обзорность суммы по бакету — только для сводки по всем клиентам
  // (зеркало §3.6 / решения MLC-049: клиенты пикуют в разные моменты).
  const caveat =
    scope === "all"
      ? `<p class="caveat">Сумма по бакету — обзорная оценка, а не истинный одновременный
         пик платформы: клиенты достигают пиков в разные моменты.</p>`
      : "";

  // В HTML график должен растягиваться на контейнер (`.chart-wrap`), поэтому
  // `responsive:true` (общий CHART_OPTIONS держит `false` ради детерминированного
  // offscreen-рендера PDF в фиксированный canvas). maintainAspectRatio:false уже задан.
  const chartConfig = {
    type: "line",
    data: buildChartData(data),
    options: { ...CHART_OPTIONS, responsive: true },
  };

  return new Blob(
    [
      `<!DOCTYPE html>
<html lang="ru">
<head>
<meta charset="utf-8">
<meta name="viewport" content="width=device-width, initial-scale=1">
<title>Использование лицензий — ${escapeHtml(scopeLabel)}</title>
<style>
  :root { color-scheme: light dark; }
  * { box-sizing: border-box; }
  body {
    font-family: system-ui, -apple-system, "Segoe UI", Roboto, sans-serif;
    margin: 24px auto; padding: 0 16px; max-width: 1100px; color: #0f172a;
  }
  h1 { font-size: clamp(18px, 3.5vw, 22px); margin: 0 0 4px; }
  .meta { color: #64748b; font-size: 14px; margin: 0 0 16px; }
  .stats { display: flex; flex-wrap: wrap; gap: 4px 24px; font-size: 14px; margin: 0 0 8px; }
  .stats b { font-variant-numeric: tabular-nums; }
  .caveat { color: #64748b; font-size: 13px; margin: 0 0 16px; max-width: 60ch; }
  /* Высота графика подстраивается под экран; ширину держит canvas (responsive). */
  .chart-wrap { position: relative; width: 100%; height: clamp(240px, 48vh, 380px); margin: 8px 0 24px; }
  @media (max-width: 640px) { body { margin: 12px auto; padding: 0 12px; } }
  @media (prefers-color-scheme: dark) {
    body { color: #e2e8f0; background: #0f172a; }
  }
</style>
</head>
<body>
<h1>Использование лицензий — ${escapeHtml(scopeLabel)}</h1>
<p class="meta">Период: ${escapeHtml(fmtFull(data.fromUtc))} — ${escapeHtml(fmtFull(data.toUtc))}</p>
<div class="stats">
  <span>Пик за период: <b>${data.peakConsumed} из ${data.peakLimit} (${percent}%)</b>${
    peakAt ? ` <span class="meta">(${escapeHtml(peakAt)})</span>` : ""
  }</span>
  <span>Среднее за период: <b>${round1(data.averageConsumed)}</b></span>
</div>
${caveat}
<div class="chart-wrap"><canvas id="chart"></canvas></div>
<script>${chartSrc}</script>
<script>
  const DATA = ${jsonForScript(chartConfig)};
  new Chart(document.getElementById("chart"), DATA);
</script>
</body>
</html>
`,
    ],
    { type: "text/html;charset=utf-8" }
  );
}
