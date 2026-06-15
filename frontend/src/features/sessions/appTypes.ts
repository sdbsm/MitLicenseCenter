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
 * Полный упорядоченный каталог известных типов сеансов (app-id), ключи маппинга
 * `sessions.appTypes` (MLC-167). Используется как набор опций фильтра «Тип сеанса»
 * НЕЗАВИСИМО от текущего снапшота — оператор может заранее отметить тип, которого
 * ещё нет онлайн. Незнакомые app-id из кластера, отсутствующие в каталоге,
 * добавляются к опциям отдельно (см. `useSessionsPage.appTypeOptions`).
 * Порядок осмысленный: интерактивные клиенты → серверные соединения → фоновые →
 * служебные консоли/отладчик.
 */
export const KNOWN_APP_IDS = [
  "1CV8",
  "1CV8C",
  "WebClient",
  "Designer",
  "COMConnection",
  "WSConnection",
  "HTTPServiceConnection",
  "BackgroundJob",
  "SystemBackgroundJob",
  "JobScheduler",
  "SrvrConsole",
  "COMConsole",
  "Debugger",
] as const;

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
