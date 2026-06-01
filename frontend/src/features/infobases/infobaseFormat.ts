import { format } from "date-fns";
import { ru } from "date-fns/locale";
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

export function formatInfobaseDateTime(value: string | null | undefined): string {
  if (!value) return "—";
  return format(new Date(value), "dd.MM.yyyy HH:mm", { locale: ru });
}

/** Число колонок таблицы инфобаз — для colSpan пустых/скелетон-строк. */
export function infobaseColumnCount(showTenant: boolean): number {
  return showTenant ? 8 : 7;
}
