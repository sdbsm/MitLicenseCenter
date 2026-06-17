// Типы для tsc: правило — .js (его грузит Node через eslint.config.js), но импортируется
// из .ts-теста и .js-конфига, попадающих под tsc -b. Этот .d.ts резолвит оба импорта
// (`./no-raw-status-badge.js`) без включения .js в проект (allowJs не нужен).
import type { Rule } from "eslint";

declare const rule: Rule.RuleModule;
export default rule;
