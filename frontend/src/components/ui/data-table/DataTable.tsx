import { flexRender, type Table as TanstackTable } from "@tanstack/react-table";
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from "@/components/ui/table";
import { cn } from "@/lib/utils";
import "./types";
import { DataTableToolbar } from "./DataTableToolbar";
import type { TableDensity } from "./useTableDensity";

// Плотность управляет только вертикальными паддингами ячеек/заголовков (MLC-144):
// comfortable — текущий вид (p-2 из примитива), compact — уплотнённый.
const densityCellClass: Record<TableDensity, string> = {
  comfortable: "py-2",
  compact: "py-1",
};
const densityHeadClass: Record<TableDensity, string> = {
  comfortable: "h-10",
  compact: "h-8",
};

export interface DataTableProps<TData> {
  /** Экземпляр таблицы из `useReactTable` (см. хелпер ниже или собрать вручную). */
  table: TanstackTable<TData>;
  density: TableDensity;
  onToggleDensity: () => void;
  /** Идёт ли первичная загрузка — рисуем строки-скелетоны. */
  isLoading?: boolean;
  /** Сколько строк-скелетонов рисовать (дефолт 5). */
  skeletonRows?: number;
  /** Рендер одной строки-скелетона (ячейки по колонкам страницы). */
  renderSkeletonRow?: () => React.ReactNode;
  /** Содержимое пустого состояния (иконка + заголовок + подсказка). */
  emptyState?: React.ReactNode;
  /** Подпись колонки для меню видимости. */
  columnLabel?: (columnId: string) => string;
  /** Контролы фильтров/поиска слева в тулбаре. */
  toolbarChildren?: React.ReactNode;
  /** Скрыть тулбар целиком (если странице он не нужен). */
  hideToolbar?: boolean;
}

/**
 * Общий компонент таблицы на `@tanstack/react-table` (ADR-46, MLC-144). Рендерит через
 * существующие shadcn-примитивы (`Table/TableHeader/…`) — сами примитивы не заменяются.
 * Три фичи в тулбаре: меню видимости колонок, density-toggle, (URL-фильтры — на стороне
 * вызывающего через `useUrlTableFilters`). Сортировка и пагинация настраиваются на
 * экземпляре таблицы (клиентская/серверная — выбором моделей/`manualPagination`).
 *
 * Статусы в колонках рендерятся ТОЛЬКО через `StatusBadge` (инвариант проекта).
 */
export function DataTable<TData>({
  table,
  density,
  onToggleDensity,
  isLoading = false,
  skeletonRows = 5,
  renderSkeletonRow,
  emptyState,
  columnLabel,
  toolbarChildren,
  hideToolbar = false,
}: DataTableProps<TData>) {
  const visibleColumnCount = table.getVisibleLeafColumns().length;
  const rows = table.getRowModel().rows;

  return (
    <div className="space-y-3">
      {!hideToolbar && (
        <DataTableToolbar
          table={table}
          density={density}
          onToggleDensity={onToggleDensity}
          columnLabel={columnLabel}
        >
          {toolbarChildren}
        </DataTableToolbar>
      )}

      <div className="rounded-md border">
        <Table>
          <TableHeader>
            {table.getHeaderGroups().map((headerGroup) => (
              <TableRow key={headerGroup.id}>
                {headerGroup.headers.map((header) => (
                  <TableHead
                    key={header.id}
                    className={cn(densityHeadClass[density], header.column.columnDef.meta?.headClassName)}
                  >
                    {header.isPlaceholder
                      ? null
                      : flexRender(header.column.columnDef.header, header.getContext())}
                  </TableHead>
                ))}
              </TableRow>
            ))}
          </TableHeader>
          <TableBody>
            {isLoading ? (
              Array.from({ length: skeletonRows }).map((_, idx) => (
                <TableRow key={`skeleton-${idx}`}>
                  {renderSkeletonRow
                    ? renderSkeletonRow()
                    : Array.from({ length: visibleColumnCount }).map((__, col) => (
                        <TableCell key={col} className={densityCellClass[density]}>
                          <span className="bg-muted block h-4 w-full animate-pulse rounded" />
                        </TableCell>
                      ))}
                </TableRow>
              ))
            ) : rows.length === 0 ? (
              emptyState ? (
                <TableRow>
                  <TableCell colSpan={visibleColumnCount} className="py-12">
                    {emptyState}
                  </TableCell>
                </TableRow>
              ) : null
            ) : (
              rows.map((row) => (
                <TableRow key={row.id} data-state={row.getIsSelected() ? "selected" : undefined}>
                  {row.getVisibleCells().map((cell) => (
                    <TableCell
                      key={cell.id}
                      className={cn(densityCellClass[density], cell.column.columnDef.meta?.cellClassName)}
                    >
                      {flexRender(cell.column.columnDef.cell, cell.getContext())}
                    </TableCell>
                  ))}
                </TableRow>
              ))
            )}
          </TableBody>
        </Table>
      </div>
    </div>
  );
}
