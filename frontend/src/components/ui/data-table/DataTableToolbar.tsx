import { Columns3Icon, Rows2Icon, Rows3Icon } from "lucide-react";
import { useTranslation } from "react-i18next";
import type { Table as TanstackTable } from "@tanstack/react-table";
import { Button } from "@/components/ui/button";
import {
  DropdownMenu,
  DropdownMenuCheckboxItem,
  DropdownMenuContent,
  DropdownMenuLabel,
  DropdownMenuSeparator,
  DropdownMenuTrigger,
} from "@/components/ui/dropdown-menu";
import type { TableDensity } from "./useTableDensity";

interface DataTableToolbarProps<TData> {
  table: TanstackTable<TData>;
  density: TableDensity;
  onToggleDensity: () => void;
  /** Подпись колонки для меню видимости (id колонки → человекочитаемое имя). */
  columnLabel?: (columnId: string) => string;
  /** Произвольные контролы слева в тулбаре (поиск/фильтры конкретной страницы). */
  children?: React.ReactNode;
}

/**
 * Тулбар таблицы (MLC-144): слот для фильтров страницы слева, справа — меню видимости
 * колонок (чекбоксы по `column.getCanHide()`) и переключатель плотности (density-toggle).
 */
export function DataTableToolbar<TData>({
  table,
  density,
  onToggleDensity,
  columnLabel,
  children,
}: DataTableToolbarProps<TData>) {
  const { t } = useTranslation();
  const hideableColumns = table.getAllColumns().filter((c) => c.getCanHide());

  return (
    <div className="flex flex-wrap items-center justify-between gap-3">
      <div className="flex flex-wrap items-center gap-3">{children}</div>
      <div className="flex items-center gap-2">
        <Button
          variant="outline"
          size="sm"
          onClick={onToggleDensity}
          aria-label={
            density === "comfortable" ? t("table.density.compact") : t("table.density.comfortable")
          }
          title={
            density === "comfortable" ? t("table.density.compact") : t("table.density.comfortable")
          }
        >
          {density === "comfortable" ? (
            <Rows2Icon className="size-4" />
          ) : (
            <Rows3Icon className="size-4" />
          )}
          <span className="hidden sm:inline">{t("table.density.label")}</span>
        </Button>

        {hideableColumns.length > 0 && (
          <DropdownMenu>
            <DropdownMenuTrigger asChild>
              <Button variant="outline" size="sm">
                <Columns3Icon className="size-4" />
                <span className="hidden sm:inline">{t("table.columns.label")}</span>
              </Button>
            </DropdownMenuTrigger>
            <DropdownMenuContent align="end" className="w-48">
              <DropdownMenuLabel>{t("table.columns.toggle")}</DropdownMenuLabel>
              <DropdownMenuSeparator />
              {hideableColumns.map((column) => (
                <DropdownMenuCheckboxItem
                  key={column.id}
                  className="capitalize"
                  checked={column.getIsVisible()}
                  // Закрытие меню по чекбоксу мешает быстрому скрытию нескольких колонок.
                  onSelect={(e) => e.preventDefault()}
                  onCheckedChange={(value) => column.toggleVisibility(!!value)}
                >
                  {columnLabel?.(column.id) ?? column.id}
                </DropdownMenuCheckboxItem>
              ))}
            </DropdownMenuContent>
          </DropdownMenu>
        )}
      </div>
    </div>
  );
}
