import { ArrowLeftIcon, DatabaseIcon, PlusIcon } from "lucide-react";
import { useMemo, useState } from "react";
import { useTranslation } from "react-i18next";
import { Link, useParams } from "react-router";
import { toast } from "sonner";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Skeleton } from "@/components/ui/skeleton";
import { Table, TableBody, TableCell, TableRow } from "@/components/ui/table";
import { useMe } from "@/features/auth/useAuth";
import { DeleteInfobaseDialog } from "@/features/infobases/DeleteInfobaseDialog";
import { InfobaseFormDialog } from "@/features/infobases/InfobaseFormDialog";
import { infobaseColumnCount } from "@/features/infobases/infobaseFormat";
import { InfobaseRow, InfobaseTableHeader } from "@/features/infobases/InfobaseRow";
import { ReassignInfobaseDialog } from "@/features/infobases/ReassignInfobaseDialog";
import type { InfobaseListItem } from "@/features/infobases/types";
import { useInfobases } from "@/features/infobases/useInfobases";
import { useTenants } from "./useTenants";

const NUMBER_FORMATTER = new Intl.NumberFormat("ru-RU");

export function TenantDetailPage() {
  const { t } = useTranslation();
  const { id = "" } = useParams();
  const { data: me } = useMe();
  const isAdmin = me?.roles?.includes("Admin") ?? false;

  const { data: tenantsData, isLoading: tenantsLoading } = useTenants();
  const tenant = useMemo(() => tenantsData?.items.find((tnt) => tnt.id === id), [tenantsData, id]);

  const { data, isLoading, isError, refetch } = useInfobases(id);
  const items = useMemo<InfobaseListItem[]>(() => data?.items ?? [], [data]);

  const [formOpen, setFormOpen] = useState(false);
  const [editing, setEditing] = useState<InfobaseListItem | null>(null);
  const [deleting, setDeleting] = useState<InfobaseListItem | null>(null);
  const [reassigning, setReassigning] = useState<InfobaseListItem | null>(null);

  const tenants = useMemo(() => tenantsData?.items ?? [], [tenantsData]);

  const handleOpenCreate = () => {
    setEditing(null);
    setFormOpen(true);
  };
  const handleOpenEdit = (infobase: InfobaseListItem) => {
    setEditing(infobase);
    setFormOpen(true);
  };

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
              <p className="text-muted-foreground text-sm">
                {t("tenants.detail.licenseLimit", {
                  count: NUMBER_FORMATTER.format(tenant.maxConcurrentLicenses),
                })}
              </p>
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

      <div className="rounded-md border">
        <Table>
          <InfobaseTableHeader />
          <TableBody>
            {isLoading
              ? Array.from({ length: 3 }).map((_, idx) => (
                  <TableRow key={`skeleton-${idx}`}>
                    {Array.from({ length: infobaseColumnCount(false) }).map((__, cidx) => (
                      <TableCell key={cidx}>
                        <Skeleton className="h-4 w-24" />
                      </TableCell>
                    ))}
                  </TableRow>
                ))
              : items.length === 0
                ? !isError && (
                    <TableRow>
                      <TableCell colSpan={infobaseColumnCount(false)} className="py-12">
                        <div className="flex flex-col items-center justify-center gap-3 text-center">
                          <DatabaseIcon className="text-muted-foreground size-8" />
                          <div className="space-y-1">
                            <p className="font-medium">{t("tenants.detail.empty.title")}</p>
                            <p className="text-muted-foreground text-sm">
                              {t("tenants.detail.empty.hint")}
                            </p>
                          </div>
                          {isAdmin && (
                            <Button size="sm" onClick={handleOpenCreate}>
                              <PlusIcon className="size-4" />
                              {t("infobases.actions.add")}
                            </Button>
                          )}
                        </div>
                      </TableCell>
                    </TableRow>
                  )
                : items.map((item) => (
                    <InfobaseRow
                      key={item.id}
                      item={item}
                      isAdmin={isAdmin}
                      onEdit={handleOpenEdit}
                      onDelete={setDeleting}
                      onReassign={tenants.length > 1 ? setReassigning : undefined}
                    />
                  ))}
          </TableBody>
        </Table>
      </div>

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
    </div>
  );
}
