import { getCoreRowModel, useReactTable } from "@tanstack/react-table";
import { ArrowLeftIcon, DatabaseIcon, PlusIcon } from "lucide-react";
import { useMemo, useState } from "react";
import { useTranslation } from "react-i18next";
import { Link, useParams } from "react-router";
import { toast } from "sonner";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { DataTable, useTableDensity } from "@/components/ui/data-table";
import { PaginationBar } from "@/components/PaginationBar";
import { Skeleton } from "@/components/ui/skeleton";
import { useMe } from "@/features/auth/useAuth";
import { StatusBadge } from "@/components/ui/StatusBadge";
import { quotaDisplay } from "@/lib/quota";
import { BackupsDialog } from "@/features/backups/BackupsDialog";
import { DeleteInfobaseDialog } from "@/features/infobases/DeleteInfobaseDialog";
import { InfobaseFormDialog } from "@/features/infobases/InfobaseFormDialog";
import { buildInfobaseDetailColumns } from "@/features/infobases/infobaseDetailColumns";
import { ReassignInfobaseDialog } from "@/features/infobases/ReassignInfobaseDialog";
import type { InfobaseListItem } from "@/features/infobases/types";
import { INFOBASES_PAGE_SIZE, useInfobases } from "@/features/infobases/useInfobases";
import { useAllTenants } from "./useTenants";
import { useTenantConsumption } from "./useTenantConsumption";

const NUMBER_FORMATTER = new Intl.NumberFormat("ru-RU");

export function TenantDetailPage() {
  const { t } = useTranslation();
  const { id = "" } = useParams();
  const { data: me } = useMe();
  const isAdmin = me?.roles?.includes("Admin") ?? false;

  const { data: tenantsData, isLoading: tenantsLoading } = useAllTenants();
  const tenant = useMemo(() => tenantsData?.items.find((tnt) => tnt.id === id), [tenantsData, id]);
  const { consumedByTenant, isLoading: isSnapshotLoading } = useTenantConsumption();

  const [page, setPage] = useState(1);
  const { data, isLoading, isError, isFetching, refetch } = useInfobases(
    id,
    null,
    false,
    page,
    INFOBASES_PAGE_SIZE
  );
  const items = useMemo<InfobaseListItem[]>(() => data?.items ?? [], [data]);
  const total = data?.total ?? 0;
  const totalPages = Math.max(1, Math.ceil(total / INFOBASES_PAGE_SIZE));
  const currentPage = Math.min(page, totalPages);

  const [formOpen, setFormOpen] = useState(false);
  const [editing, setEditing] = useState<InfobaseListItem | null>(null);
  const [deleting, setDeleting] = useState<InfobaseListItem | null>(null);
  const [reassigning, setReassigning] = useState<InfobaseListItem | null>(null);
  const [backupsFor, setBackupsFor] = useState<InfobaseListItem | null>(null);

  const tenants = useMemo(() => tenantsData?.items ?? [], [tenantsData]);

  const { density, toggleDensity } = useTableDensity();

  const handleOpenCreate = () => {
    setEditing(null);
    setFormOpen(true);
  };
  const handleOpenEdit = (infobase: InfobaseListItem) => {
    setEditing(infobase);
    setFormOpen(true);
  };

  const columns = useMemo(
    () =>
      buildInfobaseDetailColumns({
        t,
        isAdmin,
        canReassign: tenants.length > 1,
        onEdit: handleOpenEdit,
        onDelete: setDeleting,
        onReassign: tenants.length > 1 ? setReassigning : undefined,
        onBackups: setBackupsFor,
      }),
    [t, isAdmin, tenants.length]
  );

  const table = useReactTable({
    data: items,
    columns,
    getCoreRowModel: getCoreRowModel(),
    // Серверная пагинация: tanstack не режет данные сам.
    manualPagination: true,
    pageCount: totalPages,
  });

  const backLink = (
    <Button variant="ghost" size="sm" asChild className="-ml-2 w-fit">
      <Link to="/tenants">
        <ArrowLeftIcon className="size-4" />
        {t("tenants.detail.back")}
      </Link>
    </Button>
  );

  if (!tenantsLoading && !tenant) {
    return (
      <div className="space-y-4">
        {backLink}
        <div className="rounded-md border p-8 text-center">
          <p className="font-medium">{t("tenants.detail.notFound")}</p>
        </div>
      </div>
    );
  }

  return (
    <div className="space-y-6">
      <div className="space-y-3">
        {backLink}
        <div className="flex items-start justify-between gap-4">
          <div className="space-y-2">
            <div className="flex items-center gap-3">
              <h2 className="text-2xl font-semibold tracking-tight">
                {tenant?.name ?? <Skeleton className="h-7 w-48" />}
              </h2>
              {tenant &&
                (tenant.isActive ? (
                  <Badge className="border-transparent bg-emerald-500/15 text-emerald-700 dark:text-emerald-300">
                    {t("tenants.status.active")}
                  </Badge>
                ) : (
                  <Badge variant="secondary">{t("tenants.status.inactive")}</Badge>
                ))}
            </div>
            {tenant && (
              <div className="space-y-1">
                <p className="text-muted-foreground text-sm">
                  {t("tenants.detail.licenseLimit", {
                    count: NUMBER_FORMATTER.format(tenant.maxConcurrentLicenses),
                  })}
                </p>
                {/* Live-потребление из снапшота (MLC-122 / R6 / UX-02). */}
                {tenant.maxConcurrentLicenses <= 0 ? (
                  <p className="text-muted-foreground text-sm">
                    {t("tenants.detail.licenseConsumptionUnlimited")}
                  </p>
                ) : isSnapshotLoading ? (
                  <Skeleton className="h-4 w-48" />
                ) : (
                  (() => {
                    const consumed = consumedByTenant.get(tenant.id) ?? 0;
                    const { percent, badgeVariant, label } = quotaDisplay(
                      consumed,
                      tenant.maxConcurrentLicenses
                    );
                    return (
                      <div className="flex items-center gap-2">
                        <p className="text-muted-foreground text-sm">
                          {t("tenants.detail.licenseConsumption", {
                            consumed,
                            limit: tenant.maxConcurrentLicenses,
                            percent,
                          })}
                        </p>
                        {label && (
                          <StatusBadge variant={badgeVariant}>
                            {t(`common.quota.${label}`)}
                          </StatusBadge>
                        )}
                      </div>
                    );
                  })()
                )}
              </div>
            )}
          </div>
          {isAdmin && tenant && (
            <Button onClick={handleOpenCreate}>
              <PlusIcon className="size-4" />
              {t("infobases.actions.add")}
            </Button>
          )}
        </div>
      </div>

      {isError && (
        <div className="border-destructive/40 bg-destructive/5 rounded-md border p-4 text-sm">
          <p className="font-medium">{t("infobases.errors.loadFailed")}</p>
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
        columnLabel={(colId) => table.getColumn(colId)?.columnDef.meta?.label ?? colId}
        emptyState={
          !isError ? (
            <div className="flex flex-col items-center justify-center gap-3 text-center">
              <DatabaseIcon className="text-muted-foreground size-8" />
              <div className="space-y-1">
                <p className="font-medium">{t("tenants.detail.empty.title")}</p>
                <p className="text-muted-foreground text-sm">{t("tenants.detail.empty.hint")}</p>
              </div>
              {isAdmin && (
                <Button size="sm" onClick={handleOpenCreate}>
                  <PlusIcon className="size-4" />
                  {t("infobases.actions.add")}
                </Button>
              )}
            </div>
          ) : undefined
        }
      />

      <PaginationBar
        page={currentPage}
        pageSize={INFOBASES_PAGE_SIZE}
        total={total}
        onPageChange={setPage}
        isFetching={isFetching && !isLoading}
      />

      <InfobaseFormDialog
        key={editing?.id ?? "create"}
        open={formOpen}
        onOpenChange={setFormOpen}
        infobase={editing}
        tenants={tenants}
        defaultTenantId={id}
      />
      <DeleteInfobaseDialog
        key={deleting?.id ?? "none"}
        open={deleting !== null}
        onOpenChange={(open) => {
          if (!open) setDeleting(null);
        }}
        infobase={deleting}
      />
      <ReassignInfobaseDialog
        key={reassigning?.id ?? "no-reassign"}
        open={reassigning !== null}
        onOpenChange={(open) => {
          if (!open) setReassigning(null);
        }}
        infobase={reassigning}
        tenants={tenants}
      />
      <BackupsDialog
        key={backupsFor?.id ?? "no-backups"}
        open={backupsFor !== null}
        onOpenChange={(open) => {
          if (!open) setBackupsFor(null);
        }}
        infobase={backupsFor}
      />
    </div>
  );
}
