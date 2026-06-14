import { format } from "date-fns";
import { ru } from "date-fns/locale";
import {
  KeyRoundIcon,
  MoreHorizontalIcon,
  ShieldOffIcon,
  UserCheckIcon,
  UserCogIcon,
} from "lucide-react";
import type { ColumnDef } from "@tanstack/react-table";
import type { TFunction } from "i18next";
import { DataTableColumnHeader } from "@/components/ui/data-table";
import { Button } from "@/components/ui/button";
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuTrigger,
} from "@/components/ui/dropdown-menu";
import { StatusBadge } from "@/components/ui/StatusBadge";
import type { User } from "./types";

// Русская лексикографическая сортировка строковых колонок.
function ruStringSort(
  a: { getValue: (id: string) => unknown },
  b: { getValue: (id: string) => unknown },
  columnId: string
) {
  return String(a.getValue(columnId)).localeCompare(String(b.getValue(columnId)), "ru");
}

interface ColumnContext {
  t: TFunction;
  onResetPassword: (user: User) => void;
  onChangeRole: (user: User) => void;
  onDisable: (user: User) => void;
  onEnable: (user: User) => void;
}

/**
 * Колонки таблицы пользователей для `DataTable` (MLC-144e). Клиентская сортировка
 * через `DataTableColumnHeader`; статус — только через `StatusBadge` (инвариант).
 * Действия (меню) колонка несортируемая и не скрываемая.
 */
export function buildUserColumns(ctx: ColumnContext): ColumnDef<User>[] {
  const { t, onResetPassword, onChangeRole, onDisable, onEnable } = ctx;

  return [
    {
      id: "userName",
      accessorKey: "userName",
      header: ({ column }) => (
        <DataTableColumnHeader column={column}>{t("users.fields.userName")}</DataTableColumnHeader>
      ),
      sortingFn: ruStringSort,
      meta: { label: t("users.fields.userName"), cellClassName: "font-medium" },
      cell: ({ row }) => row.original.userName,
    },
    {
      id: "role",
      accessorKey: "roles",
      header: ({ column }) => (
        <DataTableColumnHeader column={column}>{t("users.fields.role")}</DataTableColumnHeader>
      ),
      sortingFn: ruStringSort,
      // sortingFn on array: sort by joined string
      meta: { label: t("users.fields.role"), cellClassName: "text-muted-foreground" },
      cell: ({ row }) =>
        row.original.roles
          .map((role) => t(`users.roles.${role}`, { defaultValue: role }))
          .join(", ") || "—",
    },
    {
      id: "status",
      accessorKey: "isActive",
      header: ({ column }) => (
        <DataTableColumnHeader column={column}>{t("users.fields.status")}</DataTableColumnHeader>
      ),
      sortingFn: "basic",
      meta: { label: t("users.fields.status") },
      cell: ({ row }) =>
        row.original.isActive ? (
          <StatusBadge variant="success">{t("users.status.active")}</StatusBadge>
        ) : (
          <StatusBadge variant="neutral">{t("users.status.disabled")}</StatusBadge>
        ),
    },
    {
      id: "lastLogin",
      accessorKey: "lastLoginAt",
      header: ({ column }) => (
        <DataTableColumnHeader column={column}>{t("users.fields.lastLogin")}</DataTableColumnHeader>
      ),
      sortingFn: "alphanumeric",
      meta: {
        label: t("users.fields.lastLogin"),
        cellClassName: "text-muted-foreground tabular-nums",
      },
      cell: ({ row }) =>
        row.original.lastLoginAt
          ? format(new Date(row.original.lastLoginAt), "dd.MM.yyyy HH:mm", { locale: ru })
          : "—",
    },
    {
      id: "actions",
      header: "",
      enableSorting: false,
      enableHiding: false,
      meta: { label: t("common.details"), headClassName: "w-10" },
      cell: ({ row }) => {
        const user = row.original;
        return (
          <div className="text-right">
            <DropdownMenu>
              <DropdownMenuTrigger asChild>
                <Button variant="ghost" size="icon" className="size-8">
                  <MoreHorizontalIcon className="size-4" />
                  <span className="sr-only">{t("common.details")}</span>
                </Button>
              </DropdownMenuTrigger>
              <DropdownMenuContent align="end">
                <DropdownMenuItem onSelect={() => onResetPassword(user)}>
                  <KeyRoundIcon className="size-4" />
                  {t("users.actions.resetPassword")}
                </DropdownMenuItem>
                <DropdownMenuItem onSelect={() => onChangeRole(user)}>
                  <UserCogIcon className="size-4" />
                  {t("users.actions.changeRole")}
                </DropdownMenuItem>
                {user.isActive ? (
                  <DropdownMenuItem variant="destructive" onSelect={() => onDisable(user)}>
                    <ShieldOffIcon className="size-4" />
                    {t("users.actions.disable")}
                  </DropdownMenuItem>
                ) : (
                  <DropdownMenuItem onSelect={() => onEnable(user)}>
                    <UserCheckIcon className="size-4" />
                    {t("users.actions.enable")}
                  </DropdownMenuItem>
                )}
              </DropdownMenuContent>
            </DropdownMenu>
          </div>
        );
      },
    },
  ];
}
