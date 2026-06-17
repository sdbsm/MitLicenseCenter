import js from "@eslint/js";
import globals from "globals";
import reactHooks from "eslint-plugin-react-hooks";
import reactRefresh from "eslint-plugin-react-refresh";
import tseslint from "typescript-eslint";
import noRawStatusBadge from "./eslint-rules/no-raw-status-badge.js";

const vitestGlobals = {
  describe: "readonly",
  it: "readonly",
  test: "readonly",
  expect: "readonly",
  vi: "readonly",
  beforeAll: "readonly",
  afterAll: "readonly",
  beforeEach: "readonly",
  afterEach: "readonly",
};

export default tseslint.config(
  {
    ignores: [
      "dist",
      "node_modules",
      "src/components/ui/**",
      "!src/components/ui/__tests__/**",
      "src/hooks/use-mobile.ts",
    ],
  },
  {
    extends: [js.configs.recommended, ...tseslint.configs.recommended],
    files: ["**/*.{ts,tsx}"],
    languageOptions: {
      ecmaVersion: 2022,
      globals: globals.browser,
    },
    plugins: {
      "react-hooks": reactHooks,
      "react-refresh": reactRefresh,
      mlc: { rules: { "no-raw-status-badge": noRawStatusBadge } },
    },
    rules: {
      ...reactHooks.configs.recommended.rules,
      "react-refresh/only-export-components": ["warn", { allowConstantExport: true }],
      "@typescript-eslint/consistent-type-imports": ["error", { prefer: "type-imports" }],
      "mlc/no-raw-status-badge": "error",
    },
  },
  {
    files: ["**/*.{test,spec}.{ts,tsx}", "**/__tests__/**/*.{ts,tsx}", "src/test/**/*.{ts,tsx}"],
    languageOptions: {
      globals: { ...globals.browser, ...vitestGlobals },
    },
    rules: {
      "react-refresh/only-export-components": "off",
    },
  }
);
