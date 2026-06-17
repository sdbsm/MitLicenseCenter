/**
 * Кастомное ESLint-правило `mlc/no-raw-status-badge`.
 *
 * Запрещает «сырые» цветовые классы плашки статуса в обход компонента `StatusBadge`
 * (frontend/src/components/ui/StatusBadge.tsx). `StatusBadge` — единственный канонический
 * способ нарисовать цветную плашку статуса: он гарантирует единый вид статуса на всех
 * экранах и контраст WCAG AA (MLC-138). Ручные классы дают разнобой оттенков и теряют
 * гарантию контраста — см. docs/06_UI_GUIDE.md §1, docs/ROADMAP.md.
 *
 * Сигнатура чипа — полупрозрачный фон `bg-{fam}-500/15` (варианты success/warning/danger/
 * info). Намеренно НЕ ловятся:
 *   - баннеры: `bg-{fam}-600/5`, `bg-{fam}-500/5` + рамка `border-{fam}-600/40`;
 *   - шкалы/гейджи: сплошной `bg-{fam}-500` без `/15`;
 *   - текст-подписи: `text-{fam}-600`.
 * Это разделяет статус-ЧИПЫ от законных баннеров/шкал без ложных срабатываний.
 *
 * Сам `StatusBadge.tsx` под правило не попадает — `eslint.config.js` игнорит
 * `src/components/ui/**`.
 */

const RAW_STATUS_BADGE = /bg-(?:emerald|amber|rose|sky|green|red)-500\/15/;

/** @type {import("eslint").Rule.RuleModule} */
export default {
  meta: {
    type: "problem",
    docs: {
      description:
        "Цветную плашку статуса рисует только StatusBadge (variant=success|warning|danger|info|neutral); сырые классы bg-{fam}-500/15 запрещены",
    },
    schema: [],
    messages: {
      rawStatusBadge:
        'Сырой цветовой класс плашки статуса ("{{match}}"). Используйте <StatusBadge variant=…> вместо ручных классов (docs/06_UI_GUIDE.md §1).',
    },
  },
  create(context) {
    function check(node, value) {
      if (typeof value !== "string") return;
      const m = value.match(RAW_STATUS_BADGE);
      if (m) {
        context.report({ node, messageId: "rawStatusBadge", data: { match: m[0] } });
      }
    }
    return {
      // Строковые литералы: className="… bg-rose-500/15 …", возврат строки из хелпера.
      Literal(node) {
        check(node, node.value);
      },
      // Шаблонные строки: `… bg-${...} …` — проверяем статические сегменты.
      TemplateElement(node) {
        check(node, node.value.cooked);
      },
    };
  },
};
