import { MonitorPlayIcon } from "lucide-react";
import { useTranslation } from "react-i18next";
import type { Table as TanstackTable } from "@tanstack/react-table";
import { DataTable, type TableDensity } from "@/components/ui/data-table";
import { Skeleton } from "@/components/ui/skeleton";
import { TableCell } from "@/components/ui/table";
import type { SessionSnapshotEntry } from "./types";

interface SessionsTableProps {
  table: TanstackTable<SessionSnapshotEntry>;
  isLoading: boolean;
  isError: boolean;
  isAdmin: boolean;
  density: TableDensity;
  onToggleDensity: () => void;
  /** Контролы фильтров слева в тулбаре (поиск/инфобаза). */
  filters?: React.ReactNode;
}

/**
 * Таблица сеансов поверх `DataTable` (MLC-144): клиентская сортировка/пагинация на
 * экземпляре tanstack-таблицы из `useSessionsPage`, тулбар с видимостью колонок и
 * density-toggle, фильтры страницы — в слоте `filters`.
 */
export function SessionsTable({
  table,
  isLoading,
  isError,
  isAdmin,
  density,
  onToggleDensity,
  filters,
}: SessionsTableProps) {
  const { t } = useTranslation();
  const colCount = isAdmin ? 9 : 8;

  return (
    <DataTable
      table={table}
      density={density}
      onToggleDensity={onToggleDensity}
      isLoading={isLoading}
      skeletonRows={5}
      columnLabel={(id) => table.getColumn(id)?.columnDef.meta?.label ?? id}
      toolbarChildren={filters}
      renderSkeletonRow={() => (
        <>
          {Array.from({ length: colCount }).map((_, col) => (
            <TableCell key={col}>
              <Skeleton className="h-4 w-full" />
            </TableCell>
          ))}
        </>
      )}
      emptyState={
        !isError ? (
          <div className="flex flex-col items-center justify-center gap-3 text-center">
            <MonitorPlayIcon className="text-muted-foreground size-8" />
            <div className="space-y-1">
              <p className="font-medium">{t("sessions.empty.title")}</p>
              <p className="text-muted-foreground text-sm">{t("sessions.empty.hint")}</p>
            </div>
          </div>
        ) : undefined
      }
    />
  );
}
