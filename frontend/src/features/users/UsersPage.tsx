import { PlusIcon, UsersRoundIcon } from "lucide-react";
import { useMemo, useState } from "react";
import { useTranslation } from "react-i18next";
import { toast } from "sonner";
import {
  getCoreRowModel,
  getSortedRowModel,
  useReactTable,
  type SortingState,
} from "@tanstack/react-table";
import { DataTable, useTableDensity } from "@/components/ui/data-table";
import { Button } from "@/components/ui/button";
import { Skeleton } from "@/components/ui/skeleton";
import { TableCell } from "@/components/ui/table";
import { ChangeRoleDialog } from "./ChangeRoleDialog";
import { DisableUserDialog } from "./DisableUserDialog";
import { EnableUserDialog } from "./EnableUserDialog";
import { GeneratedPasswordDialog } from "./GeneratedPasswordDialog";
import { ResetPasswordDialog } from "./ResetPasswordDialog";
import type { User } from "./types";
import { buildUserColumns } from "./userColumns";
import { UserFormDialog } from "./UserFormDialog";
import { useUsers } from "./useUsers";

interface GeneratedPassword {
  userName: string;
  password: string;
}

/**
 * Страница управления учётными записями панели (MLC-144e). Таблица построена
 * на `DataTable` (@tanstack/react-table) с клиентской сортировкой
 * (`getSortedRowModel`). Меню видимости колонок и density — в тулбаре `DataTable`.
 * Пагинация не нужна: список учёток небольшой (весь в памяти).
 */
export function UsersPage() {
  const { t } = useTranslation();
  const { data, isLoading, isError, refetch } = useUsers();

  const [formOpen, setFormOpen] = useState(false);
  const [resetting, setResetting] = useState<User | null>(null);
  const [disabling, setDisabling] = useState<User | null>(null);
  const [enabling, setEnabling] = useState<User | null>(null);
  const [changingRole, setChangingRole] = useState<User | null>(null);
  const [generated, setGenerated] = useState<GeneratedPassword | null>(null);
  const [sorting, setSorting] = useState<SortingState>([]);

  const { density, toggleDensity } = useTableDensity();

  const items = useMemo<User[]>(() => data?.items ?? [], [data]);

  const handlePasswordGenerated = (userName: string, password: string) => {
    setGenerated({ userName, password });
  };

  const columns = useMemo(
    () =>
      buildUserColumns({
        t,
        onResetPassword: setResetting,
        onChangeRole: setChangingRole,
        onDisable: setDisabling,
        onEnable: setEnabling,
      }),
    [t]
  );

  const table = useReactTable({
    data: items,
    columns,
    getCoreRowModel: getCoreRowModel(),
    getSortedRowModel: getSortedRowModel(),
    state: { sorting },
    onSortingChange: setSorting,
  });

  return (
    <div className="space-y-6">
      <div className="flex items-start justify-between gap-4">
        <div className="space-y-1">
          <h2 className="text-2xl font-semibold tracking-tight">{t("users.title")}</h2>
          <p className="text-muted-foreground text-sm">{t("users.subtitle")}</p>
        </div>
        <Button onClick={() => setFormOpen(true)}>
          <PlusIcon className="size-4" />
          {t("users.actions.add")}
        </Button>
      </div>

      {isError && (
        <div className="border-destructive/40 bg-destructive/5 rounded-md border p-4 text-sm">
          <p className="font-medium">{t("users.errors.loadFailed")}</p>
          <Button
            variant="link"
            className="px-0"
            onClick={() => {
              void refetch().then((r) => {
                if (r.isSuccess) toast.success(t("common.refresh"));
              });
            }}
          >
            {t("common.refresh")}
          </Button>
        </div>
      )}

      <DataTable
        table={table}
        density={density}
        onToggleDensity={toggleDensity}
        isLoading={isLoading}
        skeletonRows={3}
        renderSkeletonRow={() => (
          <>
            <TableCell>
              <Skeleton className="h-4 w-40" />
            </TableCell>
            <TableCell>
              <Skeleton className="h-4 w-24" />
            </TableCell>
            <TableCell>
              <Skeleton className="h-5 w-20" />
            </TableCell>
            <TableCell>
              <Skeleton className="h-4 w-32" />
            </TableCell>
            <TableCell />
          </>
        )}
        columnLabel={(id) => table.getColumn(id)?.columnDef.meta?.label ?? id}
        emptyState={
          !isError ? (
            <div className="flex flex-col items-center justify-center gap-3 text-center">
              <UsersRoundIcon className="text-muted-foreground size-8" />
              <div className="space-y-1">
                <p className="font-medium">{t("users.empty.title")}</p>
                <p className="text-muted-foreground text-sm">{t("users.empty.hint")}</p>
              </div>
              <Button size="sm" onClick={() => setFormOpen(true)}>
                <PlusIcon className="size-4" />
                {t("users.actions.add")}
              </Button>
            </div>
          ) : undefined
        }
      />

      <UserFormDialog
        open={formOpen}
        onOpenChange={setFormOpen}
        onPasswordGenerated={handlePasswordGenerated}
      />
      <ResetPasswordDialog
        key={resetting?.id ?? "reset-none"}
        user={resetting}
        open={resetting !== null}
        onOpenChange={(open) => {
          if (!open) setResetting(null);
        }}
        onPasswordGenerated={handlePasswordGenerated}
      />
      <DisableUserDialog
        key={disabling?.id ?? "disable-none"}
        user={disabling}
        open={disabling !== null}
        onOpenChange={(open) => {
          if (!open) setDisabling(null);
        }}
      />
      <EnableUserDialog
        key={enabling?.id ?? "enable-none"}
        user={enabling}
        open={enabling !== null}
        onOpenChange={(open) => {
          if (!open) setEnabling(null);
        }}
      />
      <ChangeRoleDialog
        key={changingRole?.id ?? "role-none"}
        user={changingRole}
        open={changingRole !== null}
        onOpenChange={(open) => {
          if (!open) setChangingRole(null);
        }}
      />
      {generated && (
        <GeneratedPasswordDialog
          open={generated !== null}
          onOpenChange={(open) => {
            if (!open) setGenerated(null);
          }}
          userName={generated.userName}
          password={generated.password}
        />
      )}
    </div>
  );
}
