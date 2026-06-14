import { format } from "date-fns";
import { ru } from "date-fns/locale";
import type { ColumnDef, Row, SortingFn } from "@tanstack/react-table";
import type { TFunction } from "i18next";
import { DataTableColumnHeader } from "@/components/ui/data-table";
import { Button } from "@/components/ui/button";
import { StatusBadge } from "@/components/ui/StatusBadge";
import { Tooltip, TooltipContent, TooltipTrigger } from "@/components/ui/tooltip";
import type { SessionSnapshotEntry } from "./types";

function formatDuration(seconds: number): string {
  if (seconds < 60) return `${seconds}с`;
  const minutes = Math.floor(seconds / 60);
  if (minutes < 60) {
    const secs = seconds % 60;
    return secs > 0 ? `${minutes}м ${secs}с` : `${minutes}м`;
  }
  const hours = Math.floor(minutes / 60);
  const mins = minutes % 60;
  return mins > 0 ? `${hours}ч ${mins}м` : `${hours}ч`;
}

// Русская лексикографическая сортировка строковых колонок — идентична прежней
// sortRows (UX-14): localeCompare("ru"). tanstack сам инвертирует для desc.
const ruStringSort: SortingFn<SessionSnapshotEntry> = (a, b, columnId) =>
  String(a.getValue(columnId)).localeCompare(String(b.getValue(columnId)), "ru");

// Булева колонка consumesLicense: true («считается») выше false при asc — как раньше.
const boolSort: SortingFn<SessionSnapshotEntry> = (a, b, columnId) => {
  const av = a.getValue<boolean>(columnId);
  const bv = b.getValue<boolean>(columnId);
  return av === bv ? 0 : av ? -1 : 1;
};

interface ColumnContext {
  t: TFunction;
  isAdmin: boolean;
  onKill: (session: SessionSnapshotEntry) => void;
}

/**
 * Колонки таблицы сеансов для `DataTable` (MLC-144). Клиентская сортировка по 6 ключам
 * (tanstack `getSortedRowModel`) с иконками ↑↓↕ через `DataTableColumnHeader`; компараторы
 * повторяют прежнюю семантику UX-14. Колонки sessionId/appId/action — несортируемые.
 * Статус consumesLicense — только через `StatusBadge` (инвариант).
 */
export function buildSessionColumns(ctx: ColumnContext): ColumnDef<SessionSnapshotEntry>[] {
  const { t, isAdmin, onKill } = ctx;

  const columns: ColumnDef<SessionSnapshotEntry>[] = [
    {
      id: "tenantName",
      accessorKey: "tenantName",
      header: ({ column }) => (
        <DataTableColumnHeader column={column}>{t("sessions.table.tenant")}</DataTableColumnHeader>
      ),
      sortingFn: ruStringSort,
      meta: { label: t("sessions.table.tenant"), cellClassName: "font-medium" },
      cell: ({ row }) => row.original.tenantName,
    },
    {
      id: "infobaseName",
      accessorKey: "infobaseName",
      header: ({ column }) => (
        <DataTableColumnHeader column={column}>
          {t("sessions.table.infobase")}
        </DataTableColumnHeader>
      ),
      sortingFn: ruStringSort,
      meta: { label: t("sessions.table.infobase") },
      cell: ({ row }) => row.original.infobaseName,
    },
    {
      id: "sessionId",
      accessorKey: "sessionId",
      header: t("sessions.table.sessionId"),
      enableSorting: false,
      meta: { label: t("sessions.table.sessionId"), headClassName: "w-28" },
      cell: ({ row }) => (
        <Tooltip>
          <TooltipTrigger asChild>
            <span className="cursor-help font-mono text-xs">
              {row.original.sessionId.replace(/-/g, "").slice(0, 8)}
            </span>
          </TooltipTrigger>
          <TooltipContent>
            <span className="font-mono text-xs">{row.original.sessionId}</span>
          </TooltipContent>
        </Tooltip>
      ),
    },
    {
      id: "appId",
      accessorKey: "appId",
      header: t("sessions.table.appId"),
      enableSorting: false,
      meta: {
        label: t("sessions.table.appId"),
        headClassName: "w-28",
        cellClassName: "font-mono text-xs",
      },
      cell: ({ row }) => row.original.appId,
    },
    {
      id: "userName",
      accessorKey: "userName",
      header: ({ column }) => (
        <DataTableColumnHeader column={column}>{t("sessions.table.user")}</DataTableColumnHeader>
      ),
      sortingFn: ruStringSort,
      meta: { label: t("sessions.table.user") },
      cell: ({ row }) =>
        row.original.userName.trim() ? (
          row.original.userName
        ) : (
          <span className="text-muted-foreground italic">{t("sessions.noUser")}</span>
        ),
    },
    {
      id: "startedAt",
      accessorKey: "startedAt",
      header: ({ column }) => (
        <DataTableColumnHeader column={column}>
          {t("sessions.table.startedAt")}
        </DataTableColumnHeader>
      ),
      // ISO-строки сортируются лексикографически корректно (как в прежней sortRows).
      sortingFn: ruStringSort,
      meta: {
        label: t("sessions.table.startedAt"),
        headClassName: "w-40",
        cellClassName: "text-sm tabular-nums",
      },
      cell: ({ row }) =>
        format(new Date(row.original.startedAt), "dd.MM.yyyy HH:mm:ss", { locale: ru }),
    },
    {
      id: "durationSeconds",
      accessorKey: "durationSeconds",
      header: ({ column }) => (
        <DataTableColumnHeader column={column}>
          {t("sessions.table.duration")}
        </DataTableColumnHeader>
      ),
      sortingFn: "basic",
      meta: {
        label: t("sessions.table.duration"),
        headClassName: "w-24",
        cellClassName: "text-sm tabular-nums",
      },
      cell: ({ row }) => formatDuration(row.original.durationSeconds),
    },
    {
      id: "consumesLicense",
      accessorKey: "consumesLicense",
      header: ({ column }) => (
        <DataTableColumnHeader column={column}>
          {t("sessions.table.consumesLicense")}
        </DataTableColumnHeader>
      ),
      sortingFn: boolSort,
      meta: { label: t("sessions.table.consumesLicense"), headClassName: "w-28" },
      cell: ({ row }) => (
        <StatusBadge variant={row.original.consumesLicense ? "success" : "neutral"}>
          {row.original.consumesLicense
            ? t("sessions.badges.consumesYes")
            : t("sessions.badges.consumesNo")}
        </StatusBadge>
      ),
    },
  ];

  if (isAdmin) {
    columns.push({
      id: "action",
      header: t("sessions.table.action"),
      enableSorting: false,
      enableHiding: false,
      meta: { label: t("sessions.table.action"), headClassName: "w-36" },
      cell: ({ row }: { row: Row<SessionSnapshotEntry> }) => (
        <Button size="sm" variant="outline" onClick={() => onKill(row.original)}>
          {t("sessions.kill.confirm")}
        </Button>
      ),
    });
  }

  return columns;
}
