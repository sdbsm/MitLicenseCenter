import type { TFunction } from "i18next";

/**
 * Интерактивные клиентские типы сеансов (app-id). По умолчанию страница «Сеансы»
 * показывает только их — фоновые/служебные сеансы скрыты (MLC-165). Оператор может
 * добавить любые типы через фильтр «Тип сеанса».
 */
export const INTERACTIVE_APP_IDS = [
  "1CV8",
  "1CV8C",
  "WebClient",
  "Designer",
  "COMConnection",
] as const;

const interactiveSet = new Set<string>(INTERACTIVE_APP_IDS);

export function isInteractiveAppId(appId: string): boolean {
  return interactiveSet.has(appId);
}

/**
 * Человеческое имя типа сеанса по app-id из i18n-раздела `sessions.appTypes`.
 * Неизвестный app-id (нет в маппинге) возвращается как есть — UI не падает.
 */
export function appTypeLabel(t: TFunction, appId: string): string {
  const key = `sessions.appTypes.${appId}`;
  const label = t(key);
  // i18next возвращает сам ключ, если перевода нет → показываем исходный app-id.
  return label === key ? appId : label;
}
