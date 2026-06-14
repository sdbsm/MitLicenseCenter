import { useCallback, useMemo } from "react";
import { useSearchParams } from "react-router";
import type { ColumnFiltersState, OnChangeFn, Updater } from "@tanstack/react-table";

/**
 * Префикс URL-параметров фильтров колонок, чтобы не сталкиваться с уже-существующими
 * параметрами страницы (page/pageSize/tenantId и т.п.). Колонка `name` → `?f_name=...`.
 */
const FILTER_PREFIX = "f_";

function readFilters(params: URLSearchParams): ColumnFiltersState {
  const filters: ColumnFiltersState = [];
  for (const [key, value] of params.entries()) {
    if (key.startsWith(FILTER_PREFIX) && value !== "") {
      filters.push({ id: key.slice(FILTER_PREFIX.length), value });
    }
  }
  return filters;
}

/**
 * Синхронизация `columnFilters` таблицы с `useSearchParams` (react-router): активные
 * фильтры колонок сериализуются в URL (`?f_<colId>=<value>`), чтобы отфильтрованный вид
 * шарился ссылкой (MLC-144). Поверх существующего паттерна `useSearchParams` — не трогает
 * параметры без префикса `f_`, поэтому уже-URL-фильтры (page, tenantId, q…) не ломаются.
 *
 * Значения фильтров — строки (текстовый поиск по колонке). Сложные значения вне рамок
 * пилотов 144a.
 */
export function useUrlTableFilters(): {
  columnFilters: ColumnFiltersState;
  onColumnFiltersChange: OnChangeFn<ColumnFiltersState>;
} {
  const [searchParams, setSearchParams] = useSearchParams();

  const columnFilters = useMemo(() => readFilters(searchParams), [searchParams]);

  const onColumnFiltersChange = useCallback<OnChangeFn<ColumnFiltersState>>(
    (updater: Updater<ColumnFiltersState>) => {
      const next = typeof updater === "function" ? updater(readFilters(searchParams)) : updater;
      const params = new URLSearchParams(searchParams);
      // Сносим прежние f_-ключи, оставляя чужие параметры нетронутыми.
      for (const key of [...params.keys()]) {
        if (key.startsWith(FILTER_PREFIX)) params.delete(key);
      }
      for (const f of next) {
        const value = f.value;
        if (value != null && value !== "") {
          params.set(`${FILTER_PREFIX}${f.id}`, String(value));
        }
      }
      setSearchParams(params, { replace: true });
    },
    [searchParams, setSearchParams]
  );

  return { columnFilters, onColumnFiltersChange };
}
