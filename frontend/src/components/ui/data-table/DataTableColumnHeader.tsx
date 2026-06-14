import { ArrowDownIcon, ArrowUpDownIcon, ArrowUpIcon } from "lucide-react";
import type { Column } from "@tanstack/react-table";
import { cn } from "@/lib/utils";

interface DataTableColumnHeaderProps<TData, TValue> {
  column: Column<TData, TValue>;
  children: React.ReactNode;
  className?: string;
}

/**
 * Кликабельный заголовок сортируемой колонки tanstack (MLC-144): иконки ↑ / ↓ / ↕
 * по состоянию `column.getIsSorted()` — единый вид для всех `DataTable`-списков.
 * Если колонка не сортируется (`getCanSort() === false`) — рендерит только подпись.
 */
export function DataTableColumnHeader<TData, TValue>({
  column,
  children,
  className,
}: DataTableColumnHeaderProps<TData, TValue>) {
  if (!column.getCanSort()) {
    return <span className={className}>{children}</span>;
  }

  const sorted = column.getIsSorted();

  return (
    <button
      type="button"
      onClick={() => column.toggleSorting(undefined)}
      className={cn(
        "hover:text-foreground flex items-center gap-1 transition-colors select-none",
        className
      )}
    >
      {children}
      {sorted === "asc" ? (
        <ArrowUpIcon className="size-3.5 shrink-0" />
      ) : sorted === "desc" ? (
        <ArrowDownIcon className="size-3.5 shrink-0" />
      ) : (
        <ArrowUpDownIcon className="size-3.5 shrink-0 opacity-40" />
      )}
    </button>
  );
}
