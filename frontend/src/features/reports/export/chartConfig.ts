import { format } from "date-fns";
import { ru } from "date-fns/locale";
import type { LicenseUsageSeriesResponse } from "../types";

// Единый источник конфигурации Chart.js для экспортов HTML и PDF: оба воспроизводят
// график панели (`LicenseUsageChart.tsx`) — recharts (React) в standalone-файл не
// переносится, поэтому берём vanilla Chart.js. HTML инлайнит движок и сериализует эту
// конфигурацию в JSON; PDF переиспользует её для offscreen-рендера в картинку.
//
// Палитра и семантика серий зеркалят `LicenseUsageChart.tsx`:
//  • consumedMax — заливка (наполнение пула лицензий), sky;
//  • consumedAvg — линия (средняя нагрузка в бакете), emerald;
//  • limit       — пунктирная линия-потолок, rose.
const COLOR_MAX = "#0ea5e9"; // sky-500
const COLOR_AVG = "#059669"; // emerald-600
const COLOR_LIMIT = "#f43f5e"; // rose-500

// RU-only (locked) — зеркало i18n `reports.chart.*` и легенды `LicenseUsageChart`.
// Модуль экспорта не React, useTranslation недоступен; строки фиксированы, как и
// RU-заголовки в `toCsv`/`toXlsx`.
const LABEL_MAX = "Пик потребления";
const LABEL_AVG = "Среднее потребление";
const LABEL_LIMIT = "Лимит";

function round1(value: number): number {
  return Math.round(value * 10) / 10;
}

/** Данные графика: метки оси (предформатированы date-fns/ru, как в панели,
 *  `dd.MM HH:mm`) + три ряда. JSON-сериализуемо — годится и для инлайна в HTML,
 *  и для offscreen-рендера PDF. */
export function buildChartData(data: LicenseUsageSeriesResponse) {
  const labels = data.buckets.map((b) =>
    format(new Date(b.bucketStartUtc), "dd.MM HH:mm", { locale: ru })
  );
  return {
    labels,
    datasets: [
      {
        label: LABEL_MAX,
        data: data.buckets.map((b) => b.consumedMax),
        borderColor: COLOR_MAX,
        backgroundColor: "rgba(14,165,233,0.18)",
        fill: true,
        pointRadius: 0,
        borderWidth: 2,
        cubicInterpolationMode: "monotone" as const,
      },
      {
        label: LABEL_AVG,
        // Среднее по бакету дробное — округляем до десятых для читаемости (как график).
        data: data.buckets.map((b) => round1(b.consumedAvg)),
        borderColor: COLOR_AVG,
        backgroundColor: COLOR_AVG,
        fill: false,
        pointRadius: 0,
        borderWidth: 2,
        cubicInterpolationMode: "monotone" as const,
      },
      {
        label: LABEL_LIMIT,
        data: data.buckets.map((b) => b.limit),
        borderColor: COLOR_LIMIT,
        backgroundColor: COLOR_LIMIT,
        fill: false,
        borderDash: [6, 4],
        pointRadius: 0,
        borderWidth: 2,
      },
    ],
  };
}

/** Опции графика — чистый JSON (без функций-колбэков), чтобы их можно было и
 *  сериализовать в standalone-HTML, и передать движку при offscreen-рендере.
 *  `responsive:false`/`animation:false` — для детерминированного рендера в картинку. */
export const CHART_OPTIONS = {
  responsive: false,
  animation: false as const,
  maintainAspectRatio: false,
  interaction: { mode: "index" as const, intersect: false },
  scales: {
    y: { beginAtZero: true, ticks: { precision: 0 } },
    x: { ticks: { maxRotation: 0, autoSkip: true, maxTicksLimit: 12 } },
  },
  plugins: {
    legend: { position: "top" as const },
    tooltip: { enabled: true },
  },
};
