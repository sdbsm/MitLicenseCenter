import type { TFunction } from "i18next";

// Палитра семей процессов — стабильна между гейджами/стеком/таблицей (одна семья,
// один цвет, как семантические статусы 06_UI_DESIGN §3). Незнакомый ключ из
// настраиваемого маппинга бэкенда красится цветом «Прочее».
const FAMILY_COLOR: Record<string, string> = {
  OneC: "#0ea5e9", // sky-500 — 1С
  Mssql: "#8b5cf6", // violet-500 — SQL Server
  OsUpdate: "#f59e0b", // amber-500 — обновления ОС
  Antivirus: "#ef4444", // red-500 — антивирус
  Other: "#94a3b8", // slate-400 — прочее
};

export function familyColor(family: string): string {
  return FAMILY_COLOR[family] ?? FAMILY_COLOR.Other;
}

// Подпись семьи: ключ → локализованное имя. Незнакомый ключ деградирует к самому
// ключу (маппинг настраивается оператором, новый ключ не должен ронять экран).
export function familyLabel(t: TFunction, family: string): string {
  return t(`performance.families.${family}`, { defaultValue: family });
}
