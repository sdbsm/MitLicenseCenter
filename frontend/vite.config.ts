import path from "node:path";
import { defineConfig } from "vite";
import react from "@vitejs/plugin-react";
import tailwindcss from "@tailwindcss/vite";

export default defineConfig({
  plugins: [react(), tailwindcss()],
  resolve: {
    alias: {
      "@": path.resolve(import.meta.dirname, "src"),
    },
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
            // MLC-051: тяжёлые либы экспорта отчётов (xlsx ≈ 430 кБ; в задаче B сюда
            // же лягут chart.js/jspdf). Грузятся `dynamic import` по клику (см.
            // features/reports/export/), но именованная группа держит их в отдельном
            // чанке вне общего vendor — иначе vendor перевалит за порог 500 кБ.
            // priority>vendor (специфичная раньше общей).
            {
              name: "export-libs",
              test: /node_modules[\\/](xlsx)[\\/]/,
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
