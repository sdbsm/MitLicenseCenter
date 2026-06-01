import { format } from "date-fns";
import { ru } from "date-fns/locale";
import { Building2Icon, MoreHorizontalIcon, PencilIcon, PlusIcon, Trash2Icon } from "lucide-react";
import { useMemo, useState } from "react";
import { useTranslation } from "react-i18next";
import { Link } from "react-router";
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
import { DeleteTenantDialog } from "./DeleteTenantDialog";
import { TenantFormDialog } from "./TenantFormDialog";
import type { Tenant } from "./types";
import { useTenants } from "./useTenants";

const PAGE_SIZE = 25;
const NUMBER_FORMATTER = new Intl.NumberFormat("ru-RU");

function formatDateTime(value: string | null | undefined): string {
  if (!value) return "—";
  return format(new Date(value), "dd.MM.yyyy HH:mm", { locale: ru });
}

export function TenantsPage() {
  const { t } = useTranslation();
  const { data: me } = useMe();
  const isAdmin = me?.roles?.includes("Admin") ?? false;

  const { data, isLoading, isError, refetch } = useTenants();

  const [page, setPage] = useState(1);
  const [formOpen, setFormOpen] = useState(false);
  const [editing, setEditing] = useState<Tenant | null>(null);
  const [deleting, setDeleting] = useState<Tenant | null>(null);

  const items = useMemo<Tenant[]>(() => data?.items ?? [], [data]);
  const total = items.length;
  const totalPages = Math.max(1, Math.ceil(total / PAGE_SIZE));
  const currentPage = Math.min(page, totalPages);

  const pagedItems = useMemo(
    () => items.slice((currentPage - 1) * PAGE_SIZE, currentPage * PAGE_SIZE),
    [items, currentPage]
  );

  const handleOpenCreate = () => {
    setEditing(null);
    setFormOpen(true);
  };

  const handleOpenEdit = (tenant: Tenant) => {
    setEditing(tenant);
    setFormOpen(true);
  };

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

      <div className="rounded-md border">
        <Table>
          <TableHeader>
            <TableRow>
              <TableHead>{t("tenants.fields.name")}</TableHead>
              <TableHead className="text-right">{t("tenants.fields.infobaseCount")}</TableHead>
              <TableHead className="text-right">
                {t("tenants.fields.maxConcurrentLicenses")}
              </TableHead>
              <TableHead>{t("tenants.fields.status")}</TableHead>
              <TableHead>{t("tenants.fields.createdAt")}</TableHead>
              <TableHead>{t("tenants.fields.updatedAt")}</TableHead>
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
                    <TableCell className="text-right">
                      <Skeleton className="ml-auto h-4 w-8" />
                    </TableCell>
                    <TableCell className="text-right">
                      <Skeleton className="ml-auto h-4 w-12" />
                    </TableCell>
                    <TableCell>
                      <Skeleton className="h-5 w-20" />
                    </TableCell>
                    <TableCell>
                      <Skeleton className="h-4 w-28" />
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
                      <TableCell colSpan={7} className="py-12">
                        <div className="flex flex-col items-center justify-center gap-3 text-center">
                          <Building2Icon className="text-muted-foreground size-8" />
                          <div className="space-y-1">
                            <p className="font-medium">{t("tenants.empty.title")}</p>
                            <p className="text-muted-foreground text-sm">
                              {t("tenants.empty.hint")}
                            </p>
                          </div>
                          {isAdmin && (
                            <Button size="sm" onClick={handleOpenCreate}>
                              <PlusIcon className="size-4" />
                              {t("tenants.actions.add")}
                            </Button>
                          )}
                        </div>
                      </TableCell>
                    </TableRow>
                  )
                : pagedItems.map((tenant) => (
                    <TableRow key={tenant.id}>
                      <TableCell className="font-medium">
                        <Link
                          to={`/tenants/${tenant.id}`}
                          className="hover:text-primary hover:underline"
                        >
                          {tenant.name}
                        </Link>
                      </TableCell>
                      <TableCell className="text-right tabular-nums">
                        {NUMBER_FORMATTER.format(tenant.infobaseCount)}
                      </TableCell>
                      <TableCell className="text-right tabular-nums">
                        {NUMBER_FORMATTER.format(tenant.maxConcurrentLicenses)}
                      </TableCell>
                      <TableCell>
                        {tenant.isActive ? (
                          <Badge className="border-transparent bg-emerald-500/15 text-emerald-700 dark:text-emerald-300">
                            {t("tenants.status.active")}
                          </Badge>
                        ) : (
                          <Badge variant="secondary">{t("tenants.status.inactive")}</Badge>
                        )}
                      </TableCell>
                      <TableCell className="text-muted-foreground tabular-nums">
                        {formatDateTime(tenant.createdAt)}
                      </TableCell>
                      <TableCell className="text-muted-foreground tabular-nums">
                        {formatDateTime(tenant.updatedAt)}
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
                              <DropdownMenuItem onSelect={() => handleOpenEdit(tenant)}>
                                <PencilIcon className="size-4" />
                                {t("common.edit")}
                              </DropdownMenuItem>
                              <DropdownMenuItem
                                variant="destructive"
                                onSelect={() => setDeleting(tenant)}
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
