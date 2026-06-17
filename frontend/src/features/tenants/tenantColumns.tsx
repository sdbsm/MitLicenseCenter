import { format } from "date-fns";
import { ru } from "date-fns/locale";
import { MoreHorizontalIcon, PencilIcon, Trash2Icon } from "lucide-react";
import type { ColumnDef } from "@tanstack/react-table";
import type { TFunction } from "i18next";
import { Link } from "react-router";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuTrigger,
} from "@/components/ui/dropdown-menu";
import { Skeleton } from "@/components/ui/skeleton";
import { StatusBadge } from "@/components/ui/StatusBadge";
import { quotaDisplay } from "@/lib/quota";
import type { Tenant } from "./types";

const NUMBER_FORMATTER = new Intl.NumberFormat("ru-RU");

function formatDateTime(value: string | null | undefined): string {
  if (!value) return "—";
  return format(new Date(value), "dd.MM.yyyy HH:mm", { locale: ru });
}

interface ColumnContext {
  t: TFunction;
  isAdmin: boolean;
  isSnapshotLoading: boolean;
  consumedByTenant: Map<string, number>;
  onEdit: (tenant: Tenant) => void;
  onDelete: (tenant: Tenant) => void;
}

/**
 * Колонки таблицы клиентов для `DataTable` (MLC-144). Серверная пагинация/фильтрация —
 * сортировка на сервере не применяется, поэтому колонки не сортируемые (`enableSorting:false`).
 * Колонка имени несёт `accessorKey "name"` для URL-фильтра поиска (`?f_name=`).
 * Статусы рендерятся только через `StatusBadge` (инвариант).
 */
export function buildTenantColumns(ctx: ColumnContext): ColumnDef<Tenant>[] {
  const { t, isAdmin, isSnapshotLoading, consumedByTenant, onEdit, onDelete } = ctx;

  return [
    {
      id: "name",
      accessorKey: "name",
      header: t("tenants.fields.name"),
      enableSorting: false,
      meta: { label: t("tenants.fields.name") },
      cell: ({ row }) => (
        <Link
          to={`/tenants/${row.original.id}`}
          className="hover:text-primary font-medium hover:underline"
        >
          {row.original.name}
        </Link>
      ),
    },
    {
      id: "infobaseCount",
      accessorKey: "infobaseCount",
      header: t("tenants.fields.infobaseCount"),
      enableSorting: false,
      meta: {
        label: t("tenants.fields.infobaseCount"),
        headClassName: "text-right",
        cellClassName: "text-right tabular-nums",
      },
      cell: ({ row }) => (
        <Link
          to={`/infobases?tenantId=${row.original.id}`}
          className="hover:text-primary hover:underline"
        >
          {NUMBER_FORMATTER.format(row.original.infobaseCount)}
        </Link>
      ),
    },
    {
      id: "maxConcurrentLicenses",
      accessorKey: "maxConcurrentLicenses",
      header: t("tenants.fields.maxConcurrentLicenses"),
      enableSorting: false,
      meta: {
        label: t("tenants.fields.maxConcurrentLicenses"),
        headClassName: "text-right",
        cellClassName: "text-right tabular-nums",
      },
      cell: ({ row }) => NUMBER_FORMATTER.format(row.original.maxConcurrentLicenses),
    },
    {
      id: "quota",
      header: t("tenants.quota.column"),
      enableSorting: false,
      enableHiding: true,
      meta: { label: t("tenants.quota.column") },
      cell: ({ row }) => {
        const tenant = row.original;
        if (isSnapshotLoading) return <Skeleton className="h-5 w-24" />;
        if (tenant.maxConcurrentLicenses <= 0) {
          return (
            <span className="text-muted-foreground text-sm">{t("tenants.quota.unlimited")}</span>
          );
        }
        const consumed = consumedByTenant.get(tenant.id) ?? 0;
        const { percent, badgeVariant, label } = quotaDisplay(
          consumed,
          tenant.maxConcurrentLicenses
        );
        return (
          <div className="flex items-center gap-2">
            <span className="text-muted-foreground text-sm tabular-nums">
              {t("tenants.quota.value", { consumed, limit: tenant.maxConcurrentLicenses, percent })}
            </span>
            {label && (
              <StatusBadge variant={badgeVariant}>{t(`common.quota.${label}`)}</StatusBadge>
            )}
          </div>
        );
      },
    },
    {
      id: "status",
      accessorKey: "isActive",
      header: t("tenants.fields.status"),
      enableSorting: false,
      meta: { label: t("tenants.fields.status") },
      cell: ({ row }) =>
        row.original.isActive ? (
          <Badge className="border-transparent bg-emerald-500/15 text-emerald-700 dark:text-emerald-300">
            {t("tenants.status.active")}
          </Badge>
        ) : (
          <Badge variant="secondary">{t("tenants.status.inactive")}</Badge>
        ),
    },
    {
      id: "createdAt",
      accessorKey: "createdAt",
      header: t("tenants.fields.createdAt"),
      enableSorting: false,
      meta: {
        label: t("tenants.fields.createdAt"),
        cellClassName: "text-muted-foreground tabular-nums",
      },
      cell: ({ row }) => formatDateTime(row.original.createdAt),
    },
    {
      id: "updatedAt",
      accessorKey: "updatedAt",
      header: t("tenants.fields.updatedAt"),
      enableSorting: false,
      meta: {
        label: t("tenants.fields.updatedAt"),
        cellClassName: "text-muted-foreground tabular-nums",
      },
      cell: ({ row }) => formatDateTime(row.original.updatedAt),
    },
    {
      id: "actions",
      header: "",
      enableHiding: false,
      enableSorting: false,
      meta: { headClassName: "w-10", cellClassName: "text-right" },
      cell: ({ row }) =>
        isAdmin ? (
          <DropdownMenu>
            <DropdownMenuTrigger asChild>
              <Button variant="ghost" size="icon" className="size-8">
                <MoreHorizontalIcon className="size-4" />
                <span className="sr-only">{t("common.details")}</span>
              </Button>
            </DropdownMenuTrigger>
            <DropdownMenuContent align="end">
              <DropdownMenuItem onSelect={() => onEdit(row.original)}>
                <PencilIcon className="size-4" />
                {t("common.edit")}
              </DropdownMenuItem>
              <DropdownMenuItem variant="destructive" onSelect={() => onDelete(row.original)}>
                <Trash2Icon className="size-4" />
                {t("common.delete")}
              </DropdownMenuItem>
            </DropdownMenuContent>
          </DropdownMenu>
        ) : null,
    },
  ];
}
