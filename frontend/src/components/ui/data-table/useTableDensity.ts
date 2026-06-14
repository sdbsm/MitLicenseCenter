import { useCallback, useEffect, useState } from "react";

/** Плотность строк таблицы. `comfortable` — текущий (дефолтный) вид, `compact` — уплотнённый. */
export type TableDensity = "comfortable" | "compact";

const STORAGE_KEY = "mlc-table-density";
const DEFAULT_DENSITY: TableDensity = "comfortable";

function isDensity(value: string | null): value is TableDensity {
  return value === "comfortable" || value === "compact";
}

function readStored(): TableDensity {
  if (typeof window === "undefined") return DEFAULT_DENSITY;
  try {
    const raw = window.localStorage.getItem(STORAGE_KEY);
    return isDensity(raw) ? raw : DEFAULT_DENSITY;
  } catch {
    // localStorage недоступен (private mode / SSR) — деградируем в дефолт.
    return DEFAULT_DENSITY;
  }
}

/**
 * Единый на приложение хук плотности таблиц. Значение персистится в localStorage
 * (ключ `mlc-table-density`), дефолт — `comfortable` (текущий вид). Плотность влияет
 * на вертикальные паддинги ячеек/строк в `DataTable` (MLC-144).
 */
export function useTableDensity(): {
  density: TableDensity;
  setDensity: (next: TableDensity) => void;
  toggleDensity: () => void;
} {
  const [density, setDensityState] = useState<TableDensity>(readStored);

  // Синхронизация между несколькими таблицами/вкладками: storage-событие приходит
  // от других вкладок; внутри текущей вкладки состояние обновляется напрямую.
  useEffect(() => {
    function onStorage(e: StorageEvent) {
      if (e.key === STORAGE_KEY && isDensity(e.newValue)) {
        setDensityState(e.newValue);
      }
    }
    window.addEventListener("storage", onStorage);
    return () => window.removeEventListener("storage", onStorage);
  }, []);

  const setDensity = useCallback((next: TableDensity) => {
    setDensityState(next);
    try {
      window.localStorage.setItem(STORAGE_KEY, next);
    } catch {
      // ignore — состояние всё равно применится в рамках сессии.
    }
  }, []);

  const toggleDensity = useCallback(() => {
    setDensity(density === "comfortable" ? "compact" : "comfortable");
  }, [density, setDensity]);

  return { density, setDensity, toggleDensity };
}
