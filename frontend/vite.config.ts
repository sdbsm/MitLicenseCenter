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
