import { format } from "date-fns";
import { ru } from "date-fns/locale";
import { CHART_OPTIONS } from "./chartConfig";
import type { DatabaseSizePoint } from "../types";

// Конфигурация Chart.js для экспортов размера баз (HTML + PDF). Зеркалит график
// панели `DatabaseSizeChart.tsx`: единственная серия — totalBytes (итог data+log),
// палитра sky (тот же «инфо», что у пика лицензий — раздел один). Ось Y переводим в ГБ
// (значения хранилища исчисляются гигабайтами), чтобы график был читаем без подписей
// в байтах. CHART_OPTIONS переиспользуем у лицензионного экспорта (общий
// детерминированный offscreen-рендер), переопределяя только тики оси Y под ГБ.
const COLOR_TOTAL = "#0ea5e9"; // sky-500
const GB = 1024 ** 3;

// RU-only (locked) — зеркало i18n `reports.size.chart.total` и легенды DatabaseSizeChart.
// Модуль экспорта не React, useTranslation недоступен; строка фиксирована, как и
// RU-заголовки в size-сериалайзерах.
const LABEL_TOTAL = "Общий размер";

/** Данные графика роста размера: метки оси (предформатированы date-fns/ru, `dd.MM HH:mm`)
 *  + одна серия totalBytes. JSON-сериализуемо — годится и для инлайна в HTML, и для
 *  offscreen-рендера PDF. */
export function buildSizeChartData(points: DatabaseSizePoint[]) {
  const labels = points.map((p) => format(new Date(p.atUtc), "dd.MM HH:mm", { locale: ru }));
  return {
    labels,
    datasets: [
      {
        label: LABEL_TOTAL,
        data: points.map((p) => p.totalBytes),
        borderColor: COLOR_TOTAL,
        backgroundColor: "rgba(14,165,233,0.18)",
        fill: true,
        pointRadius: 0,
        borderWidth: 2,
        cubicInterpolationMode: "monotone" as const,
      },
    ],
  };
}

/** Опции графика размера: база — общий CHART_OPTIONS (responsive:false/animation:false
 *  для детерминированной картинки), но ось Y подписана в ГБ через callback. Колбэк —
 *  функция, поэтому НЕ сериализуется в JSON: HTML собирает опции в рантайме (см. toSizeHtml),
 *  PDF передаёт объект движку напрямую. */
export const SIZE_CHART_OPTIONS = {
  ...CHART_OPTIONS,
  scales: {
    ...CHART_OPTIONS.scales,
    y: {
      beginAtZero: true,
      ticks: {
        callback: (value: number | string) => `${(Number(value) / GB).toFixed(1)} ГБ`,
      },
    },
  },
};
