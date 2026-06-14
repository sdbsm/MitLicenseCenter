import { format, formatDistanceToNow } from "date-fns";
import { ru } from "date-fns/locale";
import { ScrollTextIcon } from "lucide-react";
import { useMemo } from "react";
import { useTranslation } from "react-i18next";
import { Link } from "react-router";
import { getCoreRowModel, useReactTable, type ColumnDef } from "@tanstack/react-table";
import { Badge } from "@/components/ui/badge";
import { Skeleton } from "@/components/ui/skeleton";
import { TableCell } from "@/components/ui/table";
import { Tooltip, TooltipContent, TooltipTrigger } from "@/components/ui/tooltip";
import { DataTable, useTableDensity } from "@/components/ui/data-table";
import { type AuditActionType, type AuditEntry, type AuditFilters } from "./types";

interface AuditTableProps {
  items: AuditEntry[];
  isLoading: boolean;
  isError: boolean;
  tenantNameById: Map<string, string>;
  /** Текущие фильтры (для pageCount серверной пагинации). */
  filters: AuditFilters;
  total: number;
  totalPages: number;
  toolbarChildren?: React.ReactNode;
}

/**
 * Таблица журнала аудита на DataTable (MLC-144d, ADR-46).
 * Серверная пагинация (manualPagination), меню видимости колонок и density-toggle.
 * Фильтры и пагинация полностью управляются через auditUrlState / useAuditPage —
 * компонент не мутирует URL-параметры самостоятельно.
 */
export function AuditTable({
  items,
  isLoading,
  isError,
  tenantNameById,
  filters,
  totalPages,
  toolbarChildren,
}: AuditTableProps) {
  const { t } = useTranslation();
  const { density, toggleDensity } = useTableDensity();

  const columns = useMemo<ColumnDef<AuditEntry>[]>(
    () => buildAuditColumns(t, tenantNameById),
    [t, tenantNameById]
  );

  const table = useReactTable({
    data: items,
    columns,
    getCoreRowModel: getCoreRowModel(),
    // Серверная пагинация: tanstack не режет данные самостоятельно.
    manualPagination: true,
    pageCount: totalPages,
  });

  const columnLabel = (id: string) => table.getColumn(id)?.columnDef.meta?.label ?? id;

  return (
    <DataTable
      table={table}
      density={density}
      onToggleDensity={toggleDensity}
      isLoading={isLoading}
      skeletonRows={filters.pageSize > 10 ? 6 : 3}
      renderSkeletonRow={() => <AuditSkeletonCells />}
      emptyState={
        !isError ? (
          <div className="flex flex-col items-center justify-center gap-3 text-center">
            <ScrollTextIcon className="text-muted-foreground size-8" />
            <div className="space-y-1">
              <p className="font-medium">{t("audit.empty.title")}</p>
              <p className="text-muted-foreground text-sm">{t("audit.empty.hint")}</p>
            </div>
          </div>
        ) : undefined
      }
      columnLabel={columnLabel}
      toolbarChildren={toolbarChildren}
    />
  );
}

// ---------------------------------------------------------------------------
// Определения колонок
// ---------------------------------------------------------------------------

function buildAuditColumns(
  t: ReturnType<typeof useTranslation>["t"],
  tenantNameById: Map<string, string>
): ColumnDef<AuditEntry>[] {
  return [
    {
      id: "timestamp",
      accessorKey: "timestamp",
      header: t("audit.fields.timestamp"),
      enableSorting: false,
      enableHiding: true,
      meta: {
        label: t("audit.fields.timestamp"),
        headClassName: "w-44",
        cellClassName: "text-muted-foreground tabular-nums",
      },
      cell: ({ row }) => {
        const date = new Date(row.original.timestamp);
        const exact = format(date, "dd.MM.yyyy HH:mm:ss", { locale: ru });
        const relative = formatDistanceToNow(date, { addSuffix: true, locale: ru });
        return (
          <Tooltip>
            <TooltipTrigger asChild>
              <span className="cursor-help tabular-nums">{exact}</span>
            </TooltipTrigger>
            <TooltipContent>
              <span>{relative}</span>
            </TooltipContent>
          </Tooltip>
        );
      },
    },
    {
      id: "actionType",
      accessorKey: "actionType",
      header: t("audit.fields.actionType"),
      enableSorting: false,
      enableHiding: true,
      meta: {
        label: t("audit.fields.actionType"),
        headClassName: "w-56",
      },
      cell: ({ row }) => (
        <Badge className={actionBadgeClass(row.original.actionType)}>
          {t(`audit.actions.${row.original.actionType}`)}
        </Badge>
      ),
    },
    {
      id: "initiator",
      accessorKey: "initiator",
      header: t("audit.fields.initiator"),
      enableSorting: false,
      enableHiding: true,
      meta: {
        label: t("audit.fields.initiator"),
        headClassName: "w-40",
        cellClassName: "font-mono text-xs",
      },
      cell: ({ row }) => row.original.initiator,
    },
    {
      id: "tenant",
      header: t("audit.fields.tenant"),
      enableSorting: false,
      enableHiding: true,
      meta: {
        label: t("audit.fields.tenant"),
        headClassName: "w-48",
      },
      cell: ({ row }) =>
        row.original.tenantId ? (
          <Link
            to={`/tenants?id=${encodeURIComponent(row.original.tenantId)}`}
            className="text-primary underline-offset-2 hover:underline"
          >
            {tenantNameById.get(row.original.tenantId) ?? row.original.tenantId}
          </Link>
        ) : (
          <span className="text-muted-foreground">—</span>
        ),
    },
    {
      id: "description",
      accessorKey: "description",
      header: t("audit.fields.description"),
      enableSorting: false,
      enableHiding: true,
      meta: {
        label: t("audit.fields.description"),
        cellClassName: "text-sm",
      },
      cell: ({ row }) => row.original.description,
    },
  ];
}

// ---------------------------------------------------------------------------
// Скелетон строки (5 ячеек = количество колонок)
// ---------------------------------------------------------------------------

function AuditSkeletonCells() {
  return (
    <>
      <TableCell>
        <Skeleton className="h-4 w-32" />
      </TableCell>
      <TableCell>
        <Skeleton className="h-5 w-32" />
      </TableCell>
      <TableCell>
        <Skeleton className="h-4 w-24" />
      </TableCell>
      <TableCell>
        <Skeleton className="h-4 w-28" />
      </TableCell>
      <TableCell>
        <Skeleton className="h-4 w-72" />
      </TableCell>
    </>
  );
}

// ---------------------------------------------------------------------------
// Цвета бейджей типа действия (статусная семантика через Badge-классы)
// ---------------------------------------------------------------------------

// Цвета зеркалят семантику domain-state (docs/06_UI_GUIDE.md):
//  - Created — success (green)
//  - Updated — info (blue)
//  - Deleted — danger (rose)
//  - Auth — neutral
function actionBadgeClass(action: AuditActionType): string {
  if (action.endsWith("Created")) {
    return "border-transparent bg-emerald-500/15 text-emerald-700 dark:text-emerald-300";
  }
  if (action.endsWith("Deleted")) {
    return "border-transparent bg-rose-500/15 text-rose-700 dark:text-rose-300";
  }
  if (action.endsWith("Updated")) {
    return "border-transparent bg-sky-500/15 text-sky-700 dark:text-sky-300";
  }
  return "border-transparent bg-muted text-muted-foreground";
}
