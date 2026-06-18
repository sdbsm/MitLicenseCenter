import { format } from "date-fns";
import { ru } from "date-fns/locale";
import {
  EyeIcon,
  KeyRoundIcon,
  MoreHorizontalIcon,
  ShieldCheckIcon,
  ShieldOffIcon,
  Trash2Icon,
  UserCheckIcon,
  UserCogIcon,
} from "lucide-react";
import type { ComponentType } from "react";
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

/**
 * Вычисляет 1–2 инициала из строки `userName` (имени пользователя).
 * Если имя содержит несколько слов (разделитель — пробел или точка),
 * берутся первые буквы первых двух слов; иначе первые 1–2 символа строки.
 * Результат всегда в верхнем регистре. Пустая строка → «».
 */
export function initialsOf(userName: string): string {
  const parts = userName.split(/[\s.]+/).filter(Boolean);
  if (parts.length === 0) return "";
  if (parts.length >= 2) {
    return (parts[0][0] + parts[1][0]).toUpperCase();
  }
  // Одно слово: первые 1–2 символа самого слова (не сырой строки — иначе
  // ведущие пробелы/разделители просочились бы в инициалы).
  return parts[0].slice(0, 2).toUpperCase();
}

// Аватар-монограмма (инициалы пользователя) рендерится инлайн в ячейке имени —
// круглый монохром-кружок (`bg-muted`, дизайн-система Фазы 0), без @radix-ui/react-avatar
// и без отдельного компонента (файл — билдер колонок, не компонент-модуль).

/**
 * Возвращает компонент иконки Lucide для основной роли из массива ролей пользователя.
 * Admin → ShieldCheckIcon; Viewer → EyeIcon; иначе → null.
 */
export function roleIconFor(roles: string[]): ComponentType<{ className?: string }> | null {
  if (roles.includes("Admin")) return ShieldCheckIcon;
  if (roles.includes("Viewer")) return EyeIcon;
  return null;
}

interface ColumnContext {
  t: TFunction;
  onResetPassword: (user: User) => void;
  onChangeRole: (user: User) => void;
  onDisable: (user: User) => void;
  onEnable: (user: User) => void;
  onDelete: (user: User) => void;
}

/**
 * Колонки таблицы пользователей для `DataTable` (MLC-144e). Клиентская сортировка
 * через `DataTableColumnHeader`; статус — только через `StatusBadge` (инвариант).
 * Действия (меню) колонка несортируемая и не скрываемая.
 */
export function buildUserColumns(ctx: ColumnContext): ColumnDef<User>[] {
  const { t, onResetPassword, onChangeRole, onDisable, onEnable, onDelete } = ctx;

  return [
    {
      id: "userName",
      accessorKey: "userName",
      header: ({ column }) => (
        <DataTableColumnHeader column={column}>{t("users.fields.userName")}</DataTableColumnHeader>
      ),
      sortingFn: ruStringSort,
      meta: { label: t("users.fields.userName"), cellClassName: "font-medium" },
      cell: ({ row }) => (
        <div className="flex items-center gap-2">
          <span
            aria-hidden="true"
            className="bg-muted text-muted-foreground inline-flex size-8 shrink-0 items-center justify-center rounded-full text-xs font-medium"
          >
            {initialsOf(row.original.userName)}
          </span>
          <span className="font-medium">{row.original.userName}</span>
        </div>
      ),
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
      cell: ({ row }) => {
        const roleText =
          row.original.roles
            .map((role) => t(`users.roles.${role}`, { defaultValue: role }))
            .join(", ") || "—";
        const RoleIcon = roleIconFor(row.original.roles);
        return (
          <div className="text-muted-foreground flex items-center gap-2">
            {RoleIcon && <RoleIcon className="text-muted-foreground size-4 shrink-0" />}
            <span>{roleText}</span>
          </div>
        );
      },
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
                {/* MLC-180 — удаление показываем всегда (в отличие от взаимоисключающих
                    Отключить/Включить): необратимое жёсткое удаление учётки. */}
                <DropdownMenuItem variant="destructive" onSelect={() => onDelete(user)}>
                  <Trash2Icon className="size-4" />
                  {t("users.actions.delete")}
                </DropdownMenuItem>
              </DropdownMenuContent>
            </DropdownMenu>
          </div>
        );
      },
    },
  ];
}
