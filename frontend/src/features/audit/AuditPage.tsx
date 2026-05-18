import { format, formatDistanceToNow } from "date-fns";
import { ru } from "date-fns/locale";
import { ScrollTextIcon } from "lucide-react";
import { useMemo } from "react";
import { useTranslation } from "react-i18next";
import { Link, useSearchParams } from "react-router";
import { toast } from "sonner";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
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
import {
  Tooltip,
  TooltipContent,
  TooltipProvider,
  TooltipTrigger,
} from "@/components/ui/tooltip";
import { useTenants } from "@/features/tenants/useTenants";
import { AuditFiltersBar } from "./AuditFiltersBar";
import {
  AUDIT_ACTION_TYPES,
  AUDIT_PAGE_SIZES,
  type AuditActionType,
  type AuditEntry,
  type AuditFilters,
  type AuditPageSize,
  DEFAULT_AUDIT_PAGE_SIZE,
} from "./types";
import { useAuditLog } from "./useAuditLog";

const MAX_PAGE_LINKS = 7;

function parseFiltersFromUrl(params: URLSearchParams): AuditFilters {
  // Любая невалидная пара ключ-значение в URL → дефолт. URL остаётся
  // shareable-link'ом, но битый параметр не должен ронять страницу.
  const actionRaw = params.get("actionType");
  const action: AuditActionType | null =
    actionRaw && (AUDIT_ACTION_TYPES as readonly string[]).includes(actionRaw)
      ? (actionRaw as AuditActionType)
      : null;

  const pageRaw = Number(params.get("page"));
  const page = Number.isFinite(pageRaw) && pageRaw > 0 ? Math.floor(pageRaw) : 1;

  const sizeRaw = Number(params.get("pageSize"));
  const pageSize: AuditPageSize = (AUDIT_PAGE_SIZES as readonly number[]).includes(sizeRaw)
    ? (sizeRaw as AuditPageSize)
    : DEFAULT_AUDIT_PAGE_SIZE;

  return {
    actionType: action,
    tenantId: params.get("tenantId"),
    from: params.get("from"),
    to: params.get("to"),
    page,
    pageSize,
  };
}

function filtersToUrl(filters: AuditFilters): URLSearchParams {
  const params = new URLSearchParams();
  if (filters.actionType) params.set("actionType", filters.actionType);
  if (filters.tenantId) params.set("tenantId", filters.tenantId);
  if (filters.from) params.set("from", filters.from);
  if (filters.to) params.set("to", filters.to);
  if (filters.page !== 1) params.set("page", String(filters.page));
  if (filters.pageSize !== DEFAULT_AUDIT_PAGE_SIZE) {
    params.set("pageSize", String(filters.pageSize));
  }
  return params;
}

// Backend отдаёт ISO UTC; для фильтра нужен RFC3339 с временем (00:00 / 23:59).
// `<input type="date">` хранит «YYYY-MM-DD», превращаем в полноценный ISO.
function dateOnlyToIsoStart(date: string): string {
  return `${date}T00:00:00Z`;
}
function dateOnlyToIsoEnd(date: string): string {
  return `${date}T23:59:59Z`;
}

function buildBackendFilters(ui: AuditFilters): AuditFilters {
  return {
    ...ui,
    from: ui.from ? dateOnlyToIsoStart(ui.from) : null,
    to: ui.to ? dateOnlyToIsoEnd(ui.to) : null,
  };
}

export function AuditPage() {
  const { t } = useTranslation();
  const [searchParams, setSearchParams] = useSearchParams();

  const filters = useMemo(() => parseFiltersFromUrl(searchParams), [searchParams]);
  const backendFilters = useMemo(() => buildBackendFilters(filters), [filters]);

  const { data: tenantsData } = useTenants();
  const tenantNameById = useMemo(() => {
    const map = new Map<string, string>();
    for (const tenant of tenantsData?.items ?? []) {
      map.set(tenant.id, tenant.name);
    }
    return map;
  }, [tenantsData]);

  const { data, isLoading, isError, refetch, isFetching } = useAuditLog(backendFilters);
  const items = data?.items ?? [];
  const total = data?.total ?? 0;
  const totalPages = Math.max(1, Math.ceil(total / filters.pageSize));
  const currentPage = Math.min(filters.page, totalPages);

  const applyFilters = (next: AuditFilters) => {
    setSearchParams(filtersToUrl(next), { replace: true });
  };

  const goToPage = (page: number) => {
    if (page < 1 || page > totalPages || page === currentPage) return;
    applyFilters({ ...filters, page });
  };

  return (
    <TooltipProvider delayDuration={150}>
      <div className="space-y-6">
        <div className="flex items-start justify-between gap-4">
          <div className="space-y-1">
            <h2 className="text-2xl font-semibold tracking-tight">{t("audit.title")}</h2>
            <p className="text-muted-foreground text-sm">{t("audit.subtitle")}</p>
          </div>
        </div>

        <AuditFiltersBar filters={filters} onChange={applyFilters} />

        {isError && (
          <div className="rounded-md border border-destructive/40 bg-destructive/5 p-4 text-sm">
            <p className="font-medium">{t("audit.errors.loadFailed")}</p>
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
                <TableHead className="w-44">{t("audit.fields.timestamp")}</TableHead>
                <TableHead className="w-56">{t("audit.fields.actionType")}</TableHead>
                <TableHead className="w-40">{t("audit.fields.initiator")}</TableHead>
                <TableHead className="w-48">{t("audit.fields.tenant")}</TableHead>
                <TableHead>{t("audit.fields.description")}</TableHead>
              </TableRow>
            </TableHeader>
            <TableBody>
              {isLoading
                ? Array.from({ length: 6 }).map((_, idx) => (
                    <TableRow key={`skeleton-${idx}`}>
                      <TableCell>
                        <Skeleton className="h-4 w-32" />
                      </TableCell>
                      <TableCell>
                        <Skeleton className="h-5 w-32" />
                      </TableCell>
                      <TableCell>
                        <Skeleton className="h-4 w-24" />
                      </TableCell>
                      <TableCell>
                        <Skeleton className="h-4 w-28" />
                      </TableCell>
                      <TableCell>
                        <Skeleton className="h-4 w-72" />
                      </TableCell>
                    </TableRow>
                  ))
                : items.length === 0
                  ? !isError && (
                      <TableRow>
                        <TableCell colSpan={5} className="py-12">
                          <div className="flex flex-col items-center justify-center gap-3 text-center">
                            <ScrollTextIcon className="text-muted-foreground size-8" />
                            <div className="space-y-1">
                              <p className="font-medium">{t("audit.empty.title")}</p>
                              <p className="text-muted-foreground text-sm">
                                {t("audit.empty.hint")}
                              </p>
                            </div>
                          </div>
                        </TableCell>
                      </TableRow>
                    )
                  : items.map((entry) => (
                      <AuditRow
                        key={entry.id}
                        entry={entry}
                        tenantName={
                          entry.tenantId ? tenantNameById.get(entry.tenantId) ?? null : null
                        }
                      />
                    ))}
            </TableBody>
          </Table>
        </div>

        {total > filters.pageSize && (
          <div className="flex items-center justify-between gap-4">
            <p className="text-muted-foreground text-sm tabular-nums">
              {t("audit.pagination.summary", {
                from: (currentPage - 1) * filters.pageSize + 1,
                to: Math.min(currentPage * filters.pageSize, total),
                total,
              })}
            </p>
            <Pagination className="mx-0 w-auto justify-end">
              <PaginationContent>
                <PaginationItem>
                  <PaginationPrevious
                    aria-disabled={currentPage === 1}
                    className={
                      currentPage === 1 ? "pointer-events-none opacity-50" : undefined
                    }
                    onClick={(e) => {
                      e.preventDefault();
                      goToPage(currentPage - 1);
                    }}
                  />
                </PaginationItem>
                {pageLinkRange(currentPage, totalPages).map((p) => (
                  <PaginationItem key={p}>
                    <PaginationLink
                      isActive={p === currentPage}
                      onClick={(e) => {
                        e.preventDefault();
                        goToPage(p);
                      }}
                    >
                      {p}
                    </PaginationLink>
                  </PaginationItem>
                ))}
                <PaginationItem>
                  <PaginationNext
                    aria-disabled={currentPage === totalPages}
                    className={
                      currentPage === totalPages
                        ? "pointer-events-none opacity-50"
                        : undefined
                    }
                    onClick={(e) => {
                      e.preventDefault();
                      goToPage(currentPage + 1);
                    }}
                  />
                </PaginationItem>
              </PaginationContent>
            </Pagination>
          </div>
        )}

        {isFetching && !isLoading && (
          <p className="text-muted-foreground text-xs">{t("audit.pagination.refreshing")}</p>
        )}
      </div>
    </TooltipProvider>
  );
}

interface AuditRowProps {
  entry: AuditEntry;
  tenantName: string | null;
}

function AuditRow({ entry, tenantName }: AuditRowProps) {
  const { t } = useTranslation();
  const date = new Date(entry.timestamp);
  const exact = format(date, "dd.MM.yyyy HH:mm:ss", { locale: ru });
  const relative = formatDistanceToNow(date, { addSuffix: true, locale: ru });

  return (
    <TableRow>
      <TableCell className="text-muted-foreground tabular-nums">
        <Tooltip>
          <TooltipTrigger asChild>
            <span className="cursor-help">{relative}</span>
          </TooltipTrigger>
          <TooltipContent>
            <span className="tabular-nums">{exact}</span>
          </TooltipContent>
        </Tooltip>
      </TableCell>
      <TableCell>
        <Badge className={actionBadgeClass(entry.actionType)}>
          {t(`audit.actions.${entry.actionType}`)}
        </Badge>
      </TableCell>
      <TableCell className="font-mono text-xs">{entry.initiator}</TableCell>
      <TableCell>
        {entry.tenantId ? (
          <Link
            to={`/tenants?id=${encodeURIComponent(entry.tenantId)}`}
            className="text-primary underline-offset-2 hover:underline"
          >
            {tenantName ?? entry.tenantId}
          </Link>
        ) : (
          <span className="text-muted-foreground">—</span>
        )}
      </TableCell>
      <TableCell className="text-sm">{entry.description}</TableCell>
    </TableRow>
  );
}

function actionBadgeClass(action: AuditActionType): string {
  // Цвета зеркалят семантику domain-state (docs/06_UI_DESIGN.md):
  //  - Created — success (green)
  //  - Updated — info (blue)
  //  - Deleted — danger (rose)
  //  - Auth — neutral
  if (action.endsWith("Created")) {
    return "border-transparent bg-emerald-500/15 text-emerald-700 dark:text-emerald-300";
  }
  if (action.endsWith("Deleted")) {
    return "border-transparent bg-rose-500/15 text-rose-700 dark:text-rose-300";
  }
  if (action.endsWith("Updated")) {
    return "border-transparent bg-sky-500/15 text-sky-700 dark:text-sky-300";
  }
  return "border-transparent bg-muted text-muted-foreground";
}

function pageLinkRange(current: number, totalPages: number): number[] {
  if (totalPages <= MAX_PAGE_LINKS) {
    return Array.from({ length: totalPages }, (_, idx) => idx + 1);
  }
  const half = Math.floor(MAX_PAGE_LINKS / 2);
  let start = Math.max(1, current - half);
  let end = start + MAX_PAGE_LINKS - 1;
  if (end > totalPages) {
    end = totalPages;
    start = end - MAX_PAGE_LINKS + 1;
  }
  return Array.from({ length: MAX_PAGE_LINKS }, (_, idx) => start + idx);
}
