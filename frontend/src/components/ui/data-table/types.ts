import type { RowData } from "@tanstack/react-table";

// Расширение метаданных колонки tanstack (MLC-144): per-колоночные классы для
// заголовка/ячейки (выравнивание, ширина) и человекочитаемая подпись для меню
// видимости колонок. Параметры дженерика обязательны сигнатурой declare module.
declare module "@tanstack/react-table" {
  // eslint-disable-next-line @typescript-eslint/no-unused-vars
  interface ColumnMeta<TData extends RowData, TValue> {
    /** Дополнительный className для `<TableHead>` колонки. */
    headClassName?: string;
    /** Дополнительный className для `<TableCell>` колонки. */
    cellClassName?: string;
    /** Человекочитаемая подпись колонки для меню видимости. */
    label?: string;
  }
}
