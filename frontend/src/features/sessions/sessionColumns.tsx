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

// Колонка licenseStatus (ADR-48): порядок Consuming → NotConsuming → Pending при asc
// (считается → не считается → определяется). Числовой ранг даёт стабильный компаратор.
const licenseStatusRank: Record<SessionSnapshotEntry["licenseStatus"], number> = {
  Consuming: 0,
  NotConsuming: 1,
  Pending: 2,
};
const licenseStatusSort: SortingFn<SessionSnapshotEntry> = (a, b, columnId) =>
  licenseStatusRank[a.getValue<SessionSnapshotEntry["licenseStatus"]>(columnId)] -
  licenseStatusRank[b.getValue<SessionSnapshotEntry["licenseStatus"]>(columnId)];

interface ColumnContext {
  t: TFunction;
  isAdmin: boolean;
  onKill: (session: SessionSnapshotEntry) => void;
}

/**
 * Колонки таблицы сеансов для `DataTable` (MLC-144). Клиентская сортировка по 6 ключам
 * (tanstack `getSortedRowModel`) с иконками ↑↓↕ через `DataTableColumnHeader`; компараторы
 * повторяют прежнюю семантику UX-14. Колонки sessionId/appId/action — несортируемые.
 * Статус licenseStatus (ADR-48) — только через `StatusBadge` (инвариант).
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
      cell: ({ row }) => {
        // MLC-148: защита от «холодных»/незаданных дат (DateTime.MinValue → 01.01.0001,
        // невалидный ISO → Invalid Date). До эпохи Unix — нет данных.
        const date = new Date(row.original.startedAt);
        const ms = date.getTime();
        if (Number.isNaN(ms) || ms <= 0) {
          return <span className="text-muted-foreground">{t("common.noData")}</span>;
        }
        return format(date, "dd.MM.yyyy HH:mm:ss", { locale: ru });
      },
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
      id: "licenseStatus",
      accessorKey: "licenseStatus",
      header: ({ column }) => (
        <DataTableColumnHeader column={column}>
          {t("sessions.table.consumesLicense")}
        </DataTableColumnHeader>
      ),
      sortingFn: licenseStatusSort,
      meta: { label: t("sessions.table.consumesLicense"), headClassName: "w-28" },
      // ADR-48: трёхсостояние через StatusBadge. Consuming→success «Считается»;
      // NotConsuming→neutral «Не считается»; Pending→info «Определяется…».
      cell: ({ row }) => {
        const status = row.original.licenseStatus;
        const variant =
          status === "Consuming" ? "success" : status === "Pending" ? "info" : "neutral";
        const label =
          status === "Consuming"
            ? t("sessions.badges.consumesYes")
            : status === "Pending"
              ? t("sessions.badges.consumesPending")
              : t("sessions.badges.consumesNo");
        return <StatusBadge variant={variant}>{label}</StatusBadge>;
      },
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
