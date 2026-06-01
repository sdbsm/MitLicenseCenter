import { ChevronDownIcon, DatabaseIcon, PlusIcon } from "lucide-react";
import { useMemo, useState } from "react";
import { useTranslation } from "react-i18next";
import { toast } from "sonner";
import { Button } from "@/components/ui/button";
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
import { Table, TableBody, TableCell, TableRow } from "@/components/ui/table";
import { useMe } from "@/features/auth/useAuth";
import { useTenants } from "@/features/tenants/useTenants";
import { DeleteInfobaseDialog } from "./DeleteInfobaseDialog";
import { groupByTenant } from "./grouping";
import { InfobaseFormDialog } from "./InfobaseFormDialog";
import { infobaseColumnCount } from "./infobaseFormat";
import { InfobaseRow, InfobaseTableHeader } from "./InfobaseRow";
import { ReassignInfobaseDialog } from "./ReassignInfobaseDialog";
import type { InfobaseListItem } from "./types";
import { useInfobases } from "./useInfobases";

const PAGE_SIZE = 25;
const ALL_TENANTS = "__all__";
type ViewMode = "flat" | "grouped";

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

  const [viewMode, setViewMode] = useState<ViewMode>("flat");
  const [collapsed, setCollapsed] = useState<Set<string>>(new Set());
  const [page, setPage] = useState(1);
  const [formOpen, setFormOpen] = useState(false);
  const [editing, setEditing] = useState<InfobaseListItem | null>(null);
  const [deleting, setDeleting] = useState<InfobaseListItem | null>(null);
  const [reassigning, setReassigning] = useState<InfobaseListItem | null>(null);

  const items = useMemo<InfobaseListItem[]>(() => data?.items ?? [], [data]);
  const total = items.length;
  const totalPages = Math.max(1, Math.ceil(total / PAGE_SIZE));
  const currentPage = Math.min(page, totalPages);

  const pagedItems = useMemo(
    () => items.slice((currentPage - 1) * PAGE_SIZE, currentPage * PAGE_SIZE),
    [items, currentPage]
  );

  const groups = useMemo(
    () => (viewMode === "grouped" ? groupByTenant(items, tenantNameById) : []),
    [viewMode, items, tenantNameById]
  );

  const toggleGroup = (tenantId: string) => {
    setCollapsed((prev) => {
      const next = new Set(prev);
      if (next.has(tenantId)) next.delete(tenantId);
      else next.add(tenantId);
      return next;
    });
  };

  const handleOpenCreate = () => {
    setEditing(null);
    setFormOpen(true);
  };

  const handleOpenEdit = (infobase: InfobaseListItem) => {
    setEditing(infobase);
    setFormOpen(true);
  };

  const isEmpty = !isLoading && !isError && items.length === 0;

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

      <div className="flex flex-wrap items-center gap-3">
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
        <div className="ml-auto inline-flex rounded-md border p-0.5">
          <Button
            variant={viewMode === "flat" ? "secondary" : "ghost"}
            size="sm"
            onClick={() => setViewMode("flat")}
          >
            {t("infobases.view.flat")}
          </Button>
          <Button
            variant={viewMode === "grouped" ? "secondary" : "ghost"}
            size="sm"
            onClick={() => setViewMode("grouped")}
          >
            {t("infobases.view.grouped")}
          </Button>
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

      {isEmpty ? (
        <div className="rounded-md border">
          <div className="flex flex-col items-center justify-center gap-3 py-12 text-center">
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
        </div>
      ) : viewMode === "grouped" && !isLoading ? (
        <div className="space-y-3">
          {groups.map((group) => {
            const isCollapsed = collapsed.has(group.tenantId);
            return (
              <div key={group.tenantId} className="rounded-md border">
                <button
                  type="button"
                  onClick={() => toggleGroup(group.tenantId)}
                  aria-expanded={!isCollapsed}
                  className="flex w-full items-center gap-2 px-4 py-3 text-left text-sm font-medium"
                >
                  <ChevronDownIcon
                    className={`size-4 transition-transform ${isCollapsed ? "-rotate-90" : ""}`}
                  />
                  <span>{group.tenantName}</span>
                  <span className="text-muted-foreground tabular-nums">({group.items.length})</span>
                </button>
                {!isCollapsed && (
                  <Table>
                    <InfobaseTableHeader />
                    <TableBody>
                      {group.items.map((item) => (
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
                )}
              </div>
            );
          })}
        </div>
      ) : (
        <div className="rounded-md border">
          <Table>
            <InfobaseTableHeader showTenant />
            <TableBody>
              {isLoading
                ? Array.from({ length: 4 }).map((_, idx) => (
                    <TableRow key={`skeleton-${idx}`}>
                      {Array.from({ length: infobaseColumnCount(true) }).map((__, cidx) => (
                        <TableCell key={cidx}>
                          <Skeleton className="h-4 w-24" />
                        </TableCell>
                      ))}
                    </TableRow>
                  ))
                : pagedItems.map((item) => (
                    <InfobaseRow
                      key={item.id}
                      item={item}
                      tenantName={tenantNameById.get(item.tenantId) ?? item.tenantName}
                      isAdmin={isAdmin}
                      onEdit={handleOpenEdit}
                      onDelete={setDeleting}
                      onReassign={tenants.length > 1 ? setReassigning : undefined}
                    />
                  ))}
            </TableBody>
          </Table>
        </div>
      )}

      {viewMode === "flat" && total > PAGE_SIZE && (
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
