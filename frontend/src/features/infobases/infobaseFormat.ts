import type { InfobaseStatus } from "./types";

export function statusBadgeClass(status: InfobaseStatus): string {
  switch (status) {
    case "Active":
      return "border-transparent bg-emerald-500/15 text-emerald-700 dark:text-emerald-300";
    case "Maintenance":
      return "border-transparent bg-amber-500/15 text-amber-700 dark:text-amber-300";
    case "Suspended":
      return "border-transparent bg-rose-500/15 text-rose-700 dark:text-rose-300";
  }
}

/** Число колонок таблицы инфобаз — для colSpan пустых/скелетон-строк.
 *  База 6: Название · Статус · Публикация · Версия платформы · Проверено ·
 *  Действия; +1 «Клиент», +1 чекбоксы bulk-выбора (MLC-081). Колонка «Сервер БД»
 *  снята (MLC-082, single-host). */
export function infobaseColumnCount(showTenant: boolean, selectable = false): number {
  return 6 + (showTenant ? 1 : 0) + (selectable ? 1 : 0);
}
