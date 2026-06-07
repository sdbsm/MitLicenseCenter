/// <reference types="vite/client" />

// MLC-052: исходник UMD-сборки Chart.js как строка — отдаётся виртуальным модулем
// (плагин chartjsUmdSource в vite.config) для инлайна в офлайн-HTML экспорта отчётов.
declare module "virtual:chartjs-umd-src" {
  const source: string;
  export default source;
}
