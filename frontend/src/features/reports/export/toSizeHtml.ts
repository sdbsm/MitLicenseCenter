// Самодостаточный офлайн-HTML с тем же интерактивным графиком роста размера, что в панели
// (DatabaseSizeChart). Исходник Chart.js ИНЛАЙНИТСЯ (без CDN) — UMD-сборка авто-регистрирует
// контроллеры, поэтому в файле достаточно `new Chart(...)`. Исходник отдаёт виртуальный
// модуль (см. плагин chartjsUmdSource в vite.config; тип — src/vite-env.d.ts).
import chartSrc from "virtual:chartjs-umd-src";
import { format } from "date-fns";
import { ru } from "date-fns/locale";
import { sizeScopeLabel, type SizeExportData } from "./sizeExport";
import { buildSizeChartData } from "./sizeChartConfig";

function fmtFull(iso: string): string {
  return format(new Date(iso), "dd.MM.yyyy HH:mm", { locale: ru });
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

/** Сериализация отчёта размера в самодостаточный HTML: заголовок (разрез + период) +
 *  интерактивный график роста итога (инлайн-движок Chart.js). Ось Y и тултип
 *  форматируются в ГБ/КБ/МБ инлайн-функциями (модуль не React, formatBytes не тащим
 *  в standalone-файл — дублируем 4 строки JS). Сырую таблицу размеров презентационная
 *  выгрузка не несёт (зеркало MLC-054 для лицензий) — она есть в CSV/XLSX. */
export function toSizeHtml(data: SizeExportData): Blob {
  const scopeLabel = sizeScopeLabel(data.scope);
  const chartData = buildSizeChartData(data.points);

  return new Blob(
    [
      `<!DOCTYPE html>
<html lang="ru">
<head>
<meta charset="utf-8">
<meta name="viewport" content="width=device-width, initial-scale=1">
<title>Размер баз — ${escapeHtml(scopeLabel)}</title>
<style>
  :root { color-scheme: light dark; }
  * { box-sizing: border-box; }
  body {
    font-family: system-ui, -apple-system, "Segoe UI", Roboto, sans-serif;
    margin: 24px auto; padding: 0 16px; max-width: 1100px; color: #0f172a;
  }
  h1 { font-size: clamp(18px, 3.5vw, 22px); margin: 0 0 4px; }
  .meta { color: #64748b; font-size: 14px; margin: 0 0 16px; }
  .chart-wrap { position: relative; width: 100%; height: clamp(240px, 48vh, 380px); margin: 8px 0 24px; }
  @media (max-width: 640px) { body { margin: 12px auto; padding: 0 12px; } }
  @media (prefers-color-scheme: dark) {
    body { color: #e2e8f0; background: #0f172a; }
  }
</style>
</head>
<body>
<h1>Размер баз — ${escapeHtml(scopeLabel)}</h1>
<p class="meta">Период: ${escapeHtml(fmtFull(data.fromUtc))} — ${escapeHtml(fmtFull(data.toUtc))}</p>
<div class="chart-wrap"><canvas id="chart"></canvas></div>
<script>${chartSrc}</script>
<script>
  // Форматирование байт → КБ/МБ/ГБ (база 1024) — зеркало lib/formatBytes; в standalone
  // встроено инлайном (React-утилиту в файл не переносим).
  function formatBytes(b) {
    var gb = b / Math.pow(1024, 3);
    if (gb >= 1) return gb.toFixed(1) + " ГБ";
    var mb = b / Math.pow(1024, 2);
    if (mb >= 1) return mb.toFixed(1) + " МБ";
    return Math.round(b / 1024) + " КБ";
  }
  var DATA = ${jsonForScript(chartData)};
  new Chart(document.getElementById("chart"), {
    type: "line",
    data: DATA,
    options: {
      responsive: true,
      maintainAspectRatio: false,
      interaction: { mode: "index", intersect: false },
      scales: {
        y: { beginAtZero: true, ticks: { callback: function (v) { return formatBytes(v); } } },
        x: { ticks: { maxRotation: 0, autoSkip: true, maxTicksLimit: 12 } }
      },
      plugins: {
        legend: { position: "top" },
        tooltip: {
          enabled: true,
          callbacks: {
            label: function (ctx) { return ctx.dataset.label + ": " + formatBytes(ctx.parsed.y); }
          }
        }
      }
    }
  });
</script>
</body>
</html>
`,
    ],
    { type: "text/html;charset=utf-8" }
  );
}
