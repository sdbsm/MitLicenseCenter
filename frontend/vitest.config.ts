import { defineConfig, mergeConfig } from "vitest/config";
import viteConfig from "./vite.config";

export default mergeConfig(
  viteConfig,
  defineConfig({
    test: {
      globals: true,
      environment: "jsdom",
      // MLC-177: прибиваем часовой пояс прогона (UTC+3). Тесты локальных→UTC границ
      // отчётов (reportsUrlState) детерминированы только при фиксированном TZ; иначе
      // «местная полночь» зависит от пояса CI-раннера. Абсолютные UTC-инстанты (`…Z`)
      // в остальных тестах от TZ не зависят.
      env: { TZ: "Europe/Moscow" },
      setupFiles: ["./src/test/setup.ts"],
      css: false,
      include: ["src/**/*.{test,spec}.{ts,tsx}"],
      restoreMocks: true,
      clearMocks: true,
    },
  })
);
