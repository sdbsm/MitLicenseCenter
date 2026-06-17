/* eslint-disable mlc/no-raw-status-badge -- фикстуры намеренно содержат запрещённую сигнатуру */
import { RuleTester } from "eslint";
import { describe } from "vitest";
import rule from "../../eslint-rules/no-raw-status-badge.js";

// RuleTester под flat-config (ESLint 10). vitest globals:true даёт describe/it/expect,
// но RuleTester.run по умолчанию ищет mocha-style describe/it — привязываем явно к vitest.
RuleTester.describe = describe;
RuleTester.it = (text, fn) => {
  // vitest it доступен глобально (globals:true); RuleTester.it ждёт сигнатуру (name, fn).
  it(text, fn);
};

const ruleTester = new RuleTester({
  languageOptions: { ecmaVersion: 2022, sourceType: "module" },
});

ruleTester.run("no-raw-status-badge", rule, {
  valid: [
    // Баннер: фон /5 + рамка /40 — законно, не плашка-статус.
    { code: 'const c = "rounded-md border border-amber-600/40 bg-amber-600/5 text-amber-700";' },
    { code: 'const c = "border border-amber-500/40 bg-amber-500/5 text-amber-800";' },
    // Сплошная шкала/гейдж без /15.
    { code: 'const c = "h-2 rounded bg-emerald-500";' },
    // Текст-подпись без фона.
    { code: 'const c = "text-rose-600 font-medium";' },
    // Пилюля «на паузе» (LiveControls) — другой shade, не -500/15.
    { code: 'const c = "bg-amber-100 text-amber-700 dark:bg-amber-950";' },
    // Шаблонная строка без сигнатуры.
    { code: "const c = `flex ${gap} bg-emerald-600/5`;" },
  ],
  invalid: [
    // Прямой строковый литерал className.
    {
      code: 'const c = "border-transparent bg-rose-500/15 text-rose-700 dark:text-rose-300";',
      errors: [{ messageId: "rawStatusBadge" }],
    },
    // Каждая семья цвета ловится.
    {
      code: 'const c = "bg-emerald-500/15";',
      errors: [{ messageId: "rawStatusBadge" }],
    },
    {
      code: 'const c = "bg-amber-500/15";',
      errors: [{ messageId: "rawStatusBadge" }],
    },
    {
      code: 'const c = "bg-sky-500/15";',
      errors: [{ messageId: "rawStatusBadge" }],
    },
    {
      code: 'const c = "bg-green-500/15";',
      errors: [{ messageId: "rawStatusBadge" }],
    },
    {
      code: 'const c = "bg-red-500/15";',
      errors: [{ messageId: "rawStatusBadge" }],
    },
    // Возврат сырого класса из хелпера.
    {
      code: 'function f() { return "border-transparent bg-emerald-500/15 text-emerald-700"; }',
      errors: [{ messageId: "rawStatusBadge" }],
    },
    // Статический сегмент шаблонной строки.
    {
      code: "const c = `border-transparent bg-rose-500/15 ${extra}`;",
      errors: [{ messageId: "rawStatusBadge" }],
    },
  ],
});
