import { getCoreRowModel, useReactTable, type ColumnFiltersState } from "@tanstack/react-table";
import { Building2Icon, PlusIcon } from "lucide-react";
import { useEffect, useMemo, useState } from "react";
import { useTranslation } from "react-i18next";
import { toast } from "sonner";
import { Button } from "@/components/ui/button";
import { DataTable, useTableDensity, useUrlTableFilters } from "@/components/ui/data-table";
import { Input } from "@/components/ui/input";
import { PaginationBar } from "@/components/PaginationBar";
import { Skeleton } from "@/components/ui/skeleton";
import { TableCell } from "@/components/ui/table";
import { useMe } from "@/features/auth/useAuth";
import { DeleteTenantDialog } from "./DeleteTenantDialog";
import { buildTenantColumns } from "./tenantColumns";
import { TenantFormDialog } from "./TenantFormDialog";
import type { Tenant } from "./types";
import { TENANTS_PAGE_SIZE, useTenants } from "./useTenants";
import { useTenantConsumption } from "./useTenantConsumption";

const PAGE_SIZE = TENANTS_PAGE_SIZE;

// Поиск по имени клиента живёт в URL-фильтре колонки `name` (?f_name=) через единый
// механизм useUrlTableFilters (MLC-144) — отфильтрованный список шарится ссылкой.
function readNameFilter(filters: ColumnFiltersState): string {
  const f = filters.find((x) => x.id === "name");
  return typeof f?.value === "string" ? f.value : "";
}

export function TenantsPage() {
  const { t } = useTranslation();
  const { data: me } = useMe();
  const isAdmin = me?.roles?.includes("Admin") ?? false;

  const { density, toggleDensity } = useTableDensity();
  const { columnFilters, onColumnFiltersChange } = useUrlTableFilters();
  const urlSearch = readNameFilter(columnFilters);

  // Серверная пагинация (manualPagination): страница — локальное состояние.
  const [page, setPage] = useState(1);

  // UX-05 (MLC-130): черновик в инпуте, коммит — после debounce в URL-фильтр;
  // при смене терма возвращаемся на первую страницу (иначе вторая может оказаться пустой).
  const [searchDraft, setSearchDraft] = useState(urlSearch);
  // Внешнее изменение URL (назад/вперёд, шаринг ссылки) подтягиваем в инпут.
  useEffect(() => {
    setSearchDraft(urlSearch);
  }, [urlSearch]);
  useEffect(() => {
    const id = setTimeout(() => {
      const next = searchDraft.trim();
      if (next === urlSearch) return;
      onColumnFiltersChange(next ? [{ id: "name", value: next }] : []);
      setPage(1);
    }, 300);
    return () => clearTimeout(id);
    // urlSearch исключён намеренно: дебаунс реагирует на ввод, не на синхронизацию из URL.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [searchDraft]);

  const { data, isLoading, isError, isFetching, refetch } = useTenants(page, PAGE_SIZE, urlSearch);
  const { consumedByTenant, isLoading: isSnapshotLoading } = useTenantConsumption();

  const [formOpen, setFormOpen] = useState(false);
  const [editing, setEditing] = useState<Tenant | null>(null);
  const [deleting, setDeleting] = useState<Tenant | null>(null);

  const items = useMemo<Tenant[]>(() => data?.items ?? [], [data]);
  const total = data?.total ?? 0;
  const totalPages = Math.max(1, Math.ceil(total / PAGE_SIZE));
  const currentPage = Math.min(page, totalPages);

  const handleOpenCreate = () => {
    setEditing(null);
    setFormOpen(true);
  };
  const handleOpenEdit = (tenant: Tenant) => {
    setEditing(tenant);
    setFormOpen(true);
  };

  const columns = useMemo(
    () =>
      buildTenantColumns({
        t,
        isAdmin,
        isSnapshotLoading,
        consumedByTenant,
        onEdit: handleOpenEdit,
        onDelete: setDeleting,
      }),
    [t, isAdmin, isSnapshotLoading, consumedByTenant]
  );

  const table = useReactTable({
    data: items,
    columns,
    getCoreRowModel: getCoreRowModel(),
    // Серверная пагинация и фильтрация: tanstack не режет/не фильтрует данные сам.
    manualPagination: true,
    manualFiltering: true,
    pageCount: totalPages,
    // «Создан»/«Обновлён» скрыты по умолчанию (MLC-200), но остаются доступны через
    // меню «Колонки» DataTable (enableHiding в определении колонок).
    initialState: { columnVisibility: { createdAt: false, updatedAt: false } },
    state: { columnFilters },
    onColumnFiltersChange,
  });

  return (
    <div className="space-y-6">
      <div className="flex items-start justify-between gap-4">
        <div className="space-y-1">
          <h2 className="text-2xl font-semibold tracking-tight">{t("tenants.title")}</h2>
          <p className="text-muted-foreground text-sm">{t("tenants.subtitle")}</p>
        </div>
        {isAdmin && (
          <Button onClick={handleOpenCreate}>
            <PlusIcon className="size-4" />
            {t("tenants.actions.add")}
          </Button>
        )}
      </div>

      {isError && (
        <div className="border-destructive/40 bg-destructive/5 rounded-md border p-4 text-sm">
          <p className="font-medium">{t("tenants.errors.loadFailed")}</p>
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
        skeletonRows={4}
        columnLabel={(id) => table.getColumn(id)?.columnDef.meta?.label ?? id}
        renderSkeletonRow={() => <TenantSkeletonCells />}
        emptyState={
          !isError ? (
            <div className="flex flex-col items-center justify-center gap-3 text-center">
              <Building2Icon className="text-muted-foreground size-8" />
              <div className="space-y-1">
                <p className="font-medium">{t("tenants.empty.title")}</p>
                <p className="text-muted-foreground text-sm">{t("tenants.empty.hint")}</p>
              </div>
              {isAdmin && (
                <Button size="sm" onClick={handleOpenCreate}>
                  <PlusIcon className="size-4" />
                  {t("tenants.actions.add")}
                </Button>
              )}
            </div>
          ) : undefined
        }
        toolbarChildren={
          <Input
            type="search"
            className="max-w-sm"
            placeholder={t("tenants.searchPlaceholder")}
            value={searchDraft}
            onChange={(e) => setSearchDraft(e.target.value)}
            aria-label={t("tenants.searchPlaceholder")}
          />
        }
      />

      <PaginationBar
        page={currentPage}
        pageSize={PAGE_SIZE}
        total={total}
        onPageChange={setPage}
        isFetching={isFetching && !isLoading}
      />

      <TenantFormDialog
        key={editing?.id ?? "create"}
        open={formOpen}
        onOpenChange={setFormOpen}
        tenant={editing}
      />
      <DeleteTenantDialog
        key={deleting?.id ?? "none"}
        open={deleting !== null}
        onOpenChange={(open) => {
          if (!open) setDeleting(null);
        }}
        tenant={deleting}
      />
    </div>
  );
}

// Скелетон строки повторяет форму ВИДИМЫХ по умолчанию колонок (MLC-200):
// Клиент · Лицензии (полоса) · Базы · Статус · ⋯ (5 ячеек; «Создан»/«Обновлён»
// скрыты по умолчанию).
function TenantSkeletonCells() {
  return (
    <>
      <TableCell>
        <Skeleton className="h-4 w-40" />
      </TableCell>
      <TableCell>
        <Skeleton className="h-4 w-48" />
      </TableCell>
      <TableCell className="text-right">
        <Skeleton className="ml-auto h-4 w-8" />
      </TableCell>
      <TableCell>
        <Skeleton className="h-5 w-20" />
      </TableCell>
      <TableCell />
    </>
  );
}
