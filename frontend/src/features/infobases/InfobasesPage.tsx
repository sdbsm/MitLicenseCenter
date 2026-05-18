import { format } from "date-fns";
import { ru } from "date-fns/locale";
import {
  DatabaseIcon,
  MoreHorizontalIcon,
  PencilIcon,
  PlusIcon,
  Trash2Icon,
} from "lucide-react";
import { useMemo, useState } from "react";
import { useTranslation } from "react-i18next";
import { toast } from "sonner";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuTrigger,
} from "@/components/ui/dropdown-menu";
import {
  Pagination,
  PaginationContent,
  PaginationItem,
  PaginationLink,
  PaginationNext,
  PaginationPrevious,
} from "@/components/ui/pagination";
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@/components/ui/select";
import { Skeleton } from "@/components/ui/skeleton";
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from "@/components/ui/table";
import { useMe } from "@/features/auth/useAuth";
import { useTenants } from "@/features/tenants/useTenants";
import { DeleteInfobaseDialog } from "./DeleteInfobaseDialog";
import { InfobaseFormDialog } from "./InfobaseFormDialog";
import type { InfobaseListItem, InfobaseStatus } from "./types";
import { useInfobases } from "./useInfobases";

const PAGE_SIZE = 25;
const ALL_TENANTS = "__all__";

function formatDateTime(value: string | null | undefined): string {
  if (!value) return "—";
  return format(new Date(value), "dd.MM.yyyy HH:mm", { locale: ru });
}

function statusBadgeClass(status: InfobaseStatus): string {
  switch (status) {
    case "Active":
      return "border-transparent bg-emerald-500/15 text-emerald-700 dark:text-emerald-300";
    case "Maintenance":
      return "border-transparent bg-amber-500/15 text-amber-700 dark:text-amber-300";
    case "Suspended":
      return "border-transparent bg-rose-500/15 text-rose-700 dark:text-rose-300";
  }
}

export function InfobasesPage() {
  const { t } = useTranslation();
  const { data: me } = useMe();
  const isAdmin = me?.roles?.includes("Admin") ?? false;

  const { data: tenantsData } = useTenants();
  const tenants = useMemo(() => tenantsData?.items ?? [], [tenantsData]);
  const tenantNameById = useMemo(() => {
    const map = new Map<string, string>();
    for (const tenant of tenants) {
      map.set(tenant.id, tenant.name);
    }
    return map;
  }, [tenants]);

  const [tenantFilter, setTenantFilter] = useState<string>(ALL_TENANTS);
  const tenantIdParam = tenantFilter === ALL_TENANTS ? null : tenantFilter;

  const { data, isLoading, isError, refetch } = useInfobases(tenantIdParam);

  const [page, setPage] = useState(1);
  const [formOpen, setFormOpen] = useState(false);
  const [editing, setEditing] = useState<InfobaseListItem | null>(null);
  const [deleting, setDeleting] = useState<InfobaseListItem | null>(null);

  const items = useMemo<InfobaseListItem[]>(() => data?.items ?? [], [data]);
  const total = items.length;
  const totalPages = Math.max(1, Math.ceil(total / PAGE_SIZE));
  const currentPage = Math.min(page, totalPages);

  const pagedItems = useMemo(
    () => items.slice((currentPage - 1) * PAGE_SIZE, currentPage * PAGE_SIZE),
    [items, currentPage],
  );

  const handleOpenCreate = () => {
    setEditing(null);
    setFormOpen(true);
  };

  const handleOpenEdit = (infobase: InfobaseListItem) => {
    setEditing(infobase);
    setFormOpen(true);
  };

  return (
    <div className="space-y-6">
      <div className="flex items-start justify-between gap-4">
        <div className="space-y-1">
          <h2 className="text-2xl font-semibold tracking-tight">{t("infobases.title")}</h2>
          <p className="text-muted-foreground text-sm">{t("infobases.subtitle")}</p>
        </div>
        {isAdmin && (
          <Button onClick={handleOpenCreate} disabled={tenants.length === 0}>
            <PlusIcon className="size-4" />
            {t("infobases.actions.add")}
          </Button>
        )}
      </div>

      <div className="flex items-center gap-3">
        <Select value={tenantFilter} onValueChange={setTenantFilter}>
          <SelectTrigger className="w-72">
            <SelectValue placeholder={t("infobases.filters.tenant")} />
          </SelectTrigger>
          <SelectContent>
            <SelectItem value={ALL_TENANTS}>{t("infobases.filters.allTenants")}</SelectItem>
            {tenants.map((tenant) => (
              <SelectItem key={tenant.id} value={tenant.id}>
                {tenant.name}
              </SelectItem>
            ))}
          </SelectContent>
        </Select>
        {tenantFilter !== ALL_TENANTS && (
          <Button variant="ghost" size="sm" onClick={() => setTenantFilter(ALL_TENANTS)}>
            {t("common.reset")}
          </Button>
        )}
      </div>

      {isError && (
        <div className="rounded-md border border-destructive/40 bg-destructive/5 p-4 text-sm">
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
          <TableHeader>
            <TableRow>
              <TableHead>{t("infobases.fields.name")}</TableHead>
              <TableHead>{t("infobases.fields.tenant")}</TableHead>
              <TableHead>{t("infobases.fields.databaseServer")}</TableHead>
              <TableHead>{t("infobases.fields.databaseName")}</TableHead>
              <TableHead>{t("infobases.fields.status")}</TableHead>
              <TableHead>{t("infobases.fields.publication")}</TableHead>
              <TableHead>{t("infobases.fields.updatedAt")}</TableHead>
              <TableHead className="w-10" />
            </TableRow>
          </TableHeader>
          <TableBody>
            {isLoading
              ? Array.from({ length: 4 }).map((_, idx) => (
                  <TableRow key={`skeleton-${idx}`}>
                    <TableCell>
                      <Skeleton className="h-4 w-40" />
                    </TableCell>
                    <TableCell>
                      <Skeleton className="h-4 w-32" />
                    </TableCell>
                    <TableCell>
                      <Skeleton className="h-4 w-28" />
                    </TableCell>
                    <TableCell>
                      <Skeleton className="h-4 w-24" />
                    </TableCell>
                    <TableCell>
                      <Skeleton className="h-5 w-20" />
                    </TableCell>
                    <TableCell>
                      <Skeleton className="h-4 w-36" />
                    </TableCell>
                    <TableCell>
                      <Skeleton className="h-4 w-28" />
                    </TableCell>
                    <TableCell />
                  </TableRow>
                ))
              : pagedItems.length === 0
                ? !isError && (
                    <TableRow>
                      <TableCell colSpan={8} className="py-12">
                        <div className="flex flex-col items-center justify-center gap-3 text-center">
                          <DatabaseIcon className="text-muted-foreground size-8" />
                          <div className="space-y-1">
                            <p className="font-medium">{t("infobases.empty.title")}</p>
                            <p className="text-muted-foreground text-sm">
                              {tenants.length === 0
                                ? t("infobases.empty.noTenantsHint")
                                : t("infobases.empty.hint")}
                            </p>
                          </div>
                          {isAdmin && tenants.length > 0 && (
                            <Button size="sm" onClick={handleOpenCreate}>
                              <PlusIcon className="size-4" />
                              {t("infobases.actions.add")}
                            </Button>
                          )}
                        </div>
                      </TableCell>
                    </TableRow>
                  )
                : pagedItems.map((item) => (
                    <TableRow key={item.id}>
                      <TableCell className="font-medium">{item.name}</TableCell>
                      <TableCell className="text-muted-foreground">
                        {tenantNameById.get(item.tenantId) ?? item.tenantName}
                      </TableCell>
                      <TableCell className="text-muted-foreground tabular-nums">
                        {item.databaseServer}
                      </TableCell>
                      <TableCell className="text-muted-foreground tabular-nums">
                        {item.databaseName}
                      </TableCell>
                      <TableCell>
                        <Badge className={statusBadgeClass(item.status)}>
                          {t(`infobases.status.${item.status}`)}
                        </Badge>
                      </TableCell>
                      <TableCell className="text-muted-foreground tabular-nums">
                        <span className="font-mono text-xs">
                          {item.publication.virtualPath}
                        </span>
                        <span className="text-muted-foreground/70 ml-2 text-xs">
                          {item.publication.platformVersion}
                        </span>
                      </TableCell>
                      <TableCell className="text-muted-foreground tabular-nums">
                        {formatDateTime(item.updatedAt ?? item.createdAt)}
                      </TableCell>
                      <TableCell className="text-right">
                        {isAdmin && (
                          <DropdownMenu>
                            <DropdownMenuTrigger asChild>
                              <Button variant="ghost" size="icon" className="size-8">
                                <MoreHorizontalIcon className="size-4" />
                                <span className="sr-only">{t("common.details")}</span>
                              </Button>
                            </DropdownMenuTrigger>
                            <DropdownMenuContent align="end">
                              <DropdownMenuItem onSelect={() => handleOpenEdit(item)}>
                                <PencilIcon className="size-4" />
                                {t("common.edit")}
                              </DropdownMenuItem>
                              <DropdownMenuItem
                                variant="destructive"
                                onSelect={() => setDeleting(item)}
                              >
                                <Trash2Icon className="size-4" />
                                {t("common.delete")}
                              </DropdownMenuItem>
                            </DropdownMenuContent>
                          </DropdownMenu>
                        )}
                      </TableCell>
                    </TableRow>
                  ))}
          </TableBody>
        </Table>
      </div>

      {total > PAGE_SIZE && (
        <Pagination>
          <PaginationContent>
            <PaginationItem>
              <PaginationPrevious
                aria-disabled={currentPage === 1}
                className={currentPage === 1 ? "pointer-events-none opacity-50" : undefined}
                onClick={(e) => {
                  e.preventDefault();
                  if (currentPage > 1) setPage(currentPage - 1);
                }}
              />
            </PaginationItem>
            {Array.from({ length: totalPages }).map((_, idx) => {
              const p = idx + 1;
              return (
                <PaginationItem key={p}>
                  <PaginationLink
                    isActive={p === currentPage}
                    onClick={(e) => {
                      e.preventDefault();
                      setPage(p);
                    }}
                  >
                    {p}
                  </PaginationLink>
                </PaginationItem>
              );
            })}
            <PaginationItem>
              <PaginationNext
                aria-disabled={currentPage === totalPages}
                className={
                  currentPage === totalPages ? "pointer-events-none opacity-50" : undefined
                }
                onClick={(e) => {
                  e.preventDefault();
                  if (currentPage < totalPages) setPage(currentPage + 1);
                }}
              />
            </PaginationItem>
          </PaginationContent>
        </Pagination>
      )}

      <InfobaseFormDialog
        key={editing?.id ?? "create"}
        open={formOpen}
        onOpenChange={setFormOpen}
        infobase={editing}
        tenants={tenants}
        defaultTenantId={tenantFilter !== ALL_TENANTS ? tenantFilter : undefined}
      />
      <DeleteInfobaseDialog
        key={deleting?.id ?? "none"}
        open={deleting !== null}
        onOpenChange={(open) => {
          if (!open) setDeleting(null);
        }}
        infobase={deleting}
      />
    </div>
  );
}
