import { readFileSync } from "node:fs";
import path from "node:path";
import { defineConfig, type Plugin } from "vite";
import react from "@vitejs/plugin-react";
import tailwindcss from "@tailwindcss/vite";

// MLC-052: интерактивный HTML-экспорт инлайнит движок Chart.js (UMD-сборка авто-
// регистрирует контроллеры) как строку — открывается офлайн без CDN. Отдаём исходник
// через виртуальный модуль `virtual:chartjs-umd-src`: его `load()` читает файл из
// установленного пакета (версия не дрейфует, без вендоринга), и в отличие от
// `node_modules/...?raw` он НЕ попадает в esbuild-предбандл dev-сервера, который
// исполнял бы UMD как CJS вместо выдачи текста. Работает одинаково в dev и в сборке.
function chartjsUmdSource(): Plugin {
  const virtualId = "virtual:chartjs-umd-src";
  const resolvedId = "\0" + virtualId;
  const filePath = path.resolve(import.meta.dirname, "node_modules/chart.js/dist/chart.umd.min.js");
  return {
    name: "chartjs-umd-source",
    resolveId(id) {
      return id === virtualId ? resolvedId : null;
    },
    load(id) {
      if (id !== resolvedId) return null;
      return `export default ${JSON.stringify(readFileSync(filePath, "utf8"))};`;
    },
  };
}

export default defineConfig({
  plugins: [react(), tailwindcss(), chartjsUmdSource()],
  resolve: {
    alias: [
      { find: "@", replacement: path.resolve(import.meta.dirname, "src") },
      // MLC-052: jsPDF динамически тянет html2canvas/canvg/dompurify только в .html()/
      // SVG — мы их не используем (график → Chart.js → PNG). Подменяем на пустой модуль,
      // иначе они (≈250 кБ+) затягиваются в eager-vendor. См. jspdfOptionalStub.ts.
      {
        find: /^(html2canvas|canvg|dompurify)$/,
        replacement: path.resolve(
          import.meta.dirname,
          "src/features/reports/export/jspdfOptionalStub.ts"
        ),
      },
    ],
  },
  build: {
    // MLC-018: страницы уже разбиты по чанкам через React.lazy (см. routes/router.tsx).
    // Остаётся крупный вендорный остаток — выносим React-рантайм в отдельный, хорошо
    // кэшируемый чанк, а прочие зависимости — в общий vendor. Так оба чанка укладываются
    // под порог предупреждения «chunks larger than 500 kB» осмысленно (а не подъёмом
    // лимита). Порядок групп — по убыванию priority (специфичные раньше общего).
    rolldownOptions: {
      output: {
        codeSplitting: {
          groups: [
            // MLC-052: общий рантайм-хелпер `__vitePreload` (виртуальный модуль, нужен
            // для dynamic import) иначе ко-локуется ролдауном в ПЕРВЫЙ попавшийся
            // dynamic-target чанк (наблюдалось — в export-pdf), и entry статически тянет
            // тот тяжёлый чанк в preload (jspdf грузился/инициализировался на старте).
            // Изолируем хелпер в собственный крошечный чанк. priority выше всех.
            {
              name: "vite-preload-helper",
              test: /preload-helper/,
              priority: 100,
            },
            {
              name: "react-vendor",
              test: /node_modules[\\/](react|react-dom|react-router|scheduler)[\\/]/,
              priority: 20,
            },
            // MLC-050: recharts + его эксклюзивный груз (victory-vendor/d3, redux-toolkit,
            // immer — ~370 кБ) выносим из общего vendor в свой чанк. Иначе единый vendor
            // переваливает за 500 кБ (порог, который эта схема держит осмысленно). Чанк
            // грузится eager'ом наравне с vendor — так же, как все прочие зависимости в этой
            // сборке (полностью ленивым его не сделать: pnpm-путь `.pnpm/<pkg>/node_modules`
            // ломает negative-lookahead, а именованная группа всегда прелоадится). Список —
            // синхронно с node_modules/recharts/package.json. priority>vendor (специфичная
            // раньше общей; positive-regex ловит вложенный `node_modules/recharts`).
            {
              name: "charts",
              test: /node_modules[\\/](recharts|victory-vendor|d3-[a-z-]+|internmap|@reduxjs[\\/]toolkit|react-redux|redux|reselect|immer|decimal\.js-light)[\\/]/,
              priority: 30,
            },
            // MLC-051/052: тяжёлые либы экспорта отчётов грузятся `dynamic import`
            // по клику (см. features/reports/export/), но именованные группы держат их
            // в отдельных чанках вне общего vendor — иначе vendor перевалит за порог
            // 500 кБ. Бьём ПО ФОРМАТАМ (не один чанк): суммарно xlsx+chart.js+jspdf
            // ≈ 1.4 МБ — единый чанк сам перевалил бы порог. priority>vendor.
            {
              name: "export-xlsx",
              test: /node_modules[\\/](xlsx)[\\/]/,
              priority: 30,
            },
            {
              name: "export-chart",
              test: /node_modules[\\/](chart\.js)[\\/]/,
              priority: 30,
            },
            {
              // Только jspdf + автотаблица. Его статические codec-зависимости (fflate,
              // fast-png, pako, iobuffer) — шарятся (fflate/pako использует и xlsx-путь);
              // если затащить их сюда, eager-чанки получают статическую кромку в export-pdf
              // и тянут его в preload. Поэтому codec'и оставляем в общем vendor (мелкие),
              // а тяжёлые рендереры html2canvas/canvg/dompurify застаблены (resolve.alias).
              name: "export-pdf",
              test: /node_modules[\\/](jspdf|jspdf-autotable)[\\/]/,
              priority: 30,
            },
            {
              name: "vendor",
              test: /node_modules[\\/]/,
              priority: 10,
            },
          ],
        },
      },
    },
  },
  server: {
    port: 5173,
    strictPort: true,
    proxy: {
      "/api": {
        target: "http://localhost:5080",
        changeOrigin: false,
      },
      "/hangfire": {
        target: "http://localhost:5080",
        changeOrigin: false,
      },
    },
  },
});
