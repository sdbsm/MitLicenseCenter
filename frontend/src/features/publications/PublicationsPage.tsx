import { Loader2Icon, ServerIcon } from "lucide-react";
import { useEffect, useMemo, useRef, useState } from "react";
import { useTranslation } from "react-i18next";
import { useSearchParams } from "react-router";
import { toast } from "sonner";
import { useQueryClient } from "@tanstack/react-query";
import { RelativeTime } from "@/components/ui/RelativeTime";
import { StatusBadge, type StatusBadgeVariant } from "@/components/ui/StatusBadge";
import { Button } from "@/components/ui/button";
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
import { ReconcilePublicationDialog } from "./ReconcilePublicationDialog";
import type { PublicationDriftStatus, PublicationListItem } from "./types";
import {
  driftStatusQueryKey,
  fetchDriftStatus,
  publicationsQueryKey,
  useCheckDrift,
  usePublications,
} from "./usePublications";

const DRIFT_VARIANT: Record<PublicationDriftStatus, StatusBadgeVariant> = {
  InSync: "success",
  Drift: "danger",
  Missing: "info",
  Error: "danger",
};

const DRIFT_STATUSES: readonly PublicationDriftStatus[] = ["InSync", "Drift", "Missing", "Error"];

function isDriftStatus(value: string): value is PublicationDriftStatus {
  return (DRIFT_STATUSES as readonly string[]).includes(value);
}

interface UrlFilters {
  tenantId: string;
  driftStatus: PublicationDriftStatus | "";
}

function parseParams(params: URLSearchParams): UrlFilters {
  const ds = params.get("driftStatus") ?? "";
  return {
    tenantId: params.get("tenantId") ?? "",
    driftStatus: isDriftStatus(ds) ? ds : "",
  };
}

const POLL_TIMEOUT_MS = 30_000;
const POLL_INTERVAL_MS = 2_000;

export function PublicationsPage() {
  const { t } = useTranslation();
  const queryClient = useQueryClient();
  const [searchParams, setSearchParams] = useSearchParams();
  const { tenantId, driftStatus } = useMemo(() => parseParams(searchParams), [searchParams]);

  const { data: publications, isLoading, isError, refetch } = usePublications();
  const { data: tenantsData } = useTenants();
  const { data: me } = useMe();
  const isAdmin = me?.roles.includes("Admin") ?? false;

  const [pollingId, setPollingId] = useState<string | null>(null);
  const pollIntervalRef = useRef<number | null>(null);
  const [selectedPublication, setSelectedPublication] = useState<PublicationListItem | null>(null);
  const [reconcileOpen, setReconcileOpen] = useState(false);

  const checkDrift = useCheckDrift();

  useEffect(() => {
    return () => {
      if (pollIntervalRef.current !== null) {
        clearInterval(pollIntervalRef.current);
        pollIntervalRef.current = null;
      }
    };
  }, []);

  const filtered = useMemo(() => {
    const rows = publications ?? [];
    return rows.filter((p) => {
      if (tenantId && p.tenantId !== tenantId) return false;
      if (driftStatus && p.lastDriftStatus !== driftStatus) return false;
      return true;
    });
  }, [publications, tenantId, driftStatus]);

  const setFilter = (next: Partial<UrlFilters>) => {
    const params = new URLSearchParams();
    const newTenantId = next.tenantId !== undefined ? next.tenantId : tenantId;
    const newDriftStatus = next.driftStatus !== undefined ? next.driftStatus : driftStatus;
    if (newTenantId) params.set("tenantId", newTenantId);
    if (newDriftStatus) params.set("driftStatus", newDriftStatus);
    setSearchParams(params, { replace: true });
  };

  const stopPolling = () => {
    if (pollIntervalRef.current !== null) {
      clearInterval(pollIntervalRef.current);
      pollIntervalRef.current = null;
    }
    setPollingId(null);
  };

  const handleCheckDrift = async (publication: PublicationListItem) => {
    if (pollIntervalRef.current !== null) {
      clearInterval(pollIntervalRef.current);
      pollIntervalRef.current = null;
    }
    try {
      await checkDrift.mutateAsync(publication.id);
    } catch {
      toast.error(t("errors.generic"));
      return;
    }
    toast.info(t("publications.toasts.checkStarted"));
    setPollingId(publication.id);

    const startedAt = Date.now();
    const initialCheckedAt = publication.lastDriftCheckAt;

    pollIntervalRef.current = window.setInterval(async () => {
      try {
        const status = await fetchDriftStatus(publication.id);
        queryClient.setQueryData(driftStatusQueryKey(publication.id), status);
        if (status.checkedAt && status.checkedAt !== initialCheckedAt) {
          stopPolling();
          void queryClient.invalidateQueries({ queryKey: publicationsQueryKey });
          toast.success(t("publications.toasts.checkCompleted"));
          return;
        }
        if (Date.now() - startedAt > POLL_TIMEOUT_MS) {
          stopPolling();
          toast.warning(t("publications.toasts.checkTimeout"));
        }
      } catch {
        stopPolling();
        toast.error(t("errors.generic"));
      }
    }, POLL_INTERVAL_MS);
  };

  const handleReconcileClick = (publication: PublicationListItem) => {
    setSelectedPublication(publication);
    setReconcileOpen(true);
  };

  const handleReconcileOpenChange = (open: boolean) => {
    setReconcileOpen(open);
    if (!open) setSelectedPublication(null);
  };

  const columnCount = isAdmin ? 10 : 9;
  const hasAnyPublications = (publications ?? []).length > 0;

  return (
    <div className="space-y-6">
      <div className="flex items-start justify-between gap-4">
        <div className="space-y-1">
          <h2 className="text-2xl font-semibold tracking-tight">{t("publications.title")}</h2>
        </div>
      </div>

      {isError && (
        <div className="border-destructive/40 bg-destructive/5 rounded-md border p-4 text-sm">
          <p className="font-medium">{t("errors.generic")}</p>
          <Button
            variant="link"
            className="px-0"
            onClick={() => {
              void refetch();
            }}
          >
            {t("common.refresh")}
          </Button>
        </div>
      )}

      <div className="flex flex-wrap gap-3">
        <Select
          value={tenantId || "_all"}
          onValueChange={(v) => setFilter({ tenantId: v === "_all" ? "" : v })}
        >
          <SelectTrigger className="w-60">
            <SelectValue placeholder={t("publications.filters.tenant")} />
          </SelectTrigger>
          <SelectContent>
            <SelectItem value="_all">{t("publications.filters.all")}</SelectItem>
            {(tenantsData?.items ?? []).map((tenant) => (
              <SelectItem key={tenant.id} value={tenant.id}>
                {tenant.name}
              </SelectItem>
            ))}
          </SelectContent>
        </Select>
        <Select
          value={driftStatus || "_all"}
          onValueChange={(v) =>
            setFilter({ driftStatus: v === "_all" ? "" : (v as PublicationDriftStatus) })
          }
        >
          <SelectTrigger className="w-52">
            <SelectValue placeholder={t("publications.filters.driftStatus")} />
          </SelectTrigger>
          <SelectContent>
            <SelectItem value="_all">{t("publications.filters.all")}</SelectItem>
            {DRIFT_STATUSES.map((status) => (
              <SelectItem key={status} value={status}>
                {t(`publications.driftStatus.${status.toLowerCase()}`)}
              </SelectItem>
            ))}
          </SelectContent>
        </Select>
      </div>

      <div className="rounded-md border">
        <Table>
          <TableHeader>
            <TableRow>
              <TableHead>{t("publications.table.headers.tenant")}</TableHead>
              <TableHead>{t("publications.table.headers.infobase")}</TableHead>
              <TableHead>{t("publications.table.headers.siteName")}</TableHead>
              <TableHead>{t("publications.table.headers.virtualPath")}</TableHead>
              <TableHead className="w-32">
                {t("publications.table.headers.platformVersion")}
              </TableHead>
              <TableHead className="w-28">{t("publications.table.headers.oData")}</TableHead>
              <TableHead className="w-28">{t("publications.table.headers.httpServices")}</TableHead>
              <TableHead className="w-36">{t("publications.table.headers.drift")}</TableHead>
              <TableHead className="w-36">{t("publications.table.headers.lastChecked")}</TableHead>
              {isAdmin && (
                <TableHead className="w-56 text-right">
                  {t("publications.table.headers.actions")}
                </TableHead>
              )}
            </TableRow>
          </TableHeader>
          <TableBody>
            {isLoading
              ? Array.from({ length: 5 }).map((_, idx) => (
                  <TableRow key={`skeleton-${idx}`}>
                    {Array.from({ length: columnCount }).map((__, col) => (
                      <TableCell key={col}>
                        <Skeleton className="h-4 w-full" />
                      </TableCell>
                    ))}
                  </TableRow>
                ))
              : filtered.length === 0
                ? !isError && (
                    <TableRow>
                      <TableCell colSpan={columnCount} className="py-12">
                        <div className="flex flex-col items-center justify-center gap-3 text-center">
                          <ServerIcon className="text-muted-foreground size-8" />
                          <p className="text-muted-foreground text-sm">
                            {hasAnyPublications
                              ? t("publications.empty.noMatchingFilter")
                              : t("publications.empty.noPublications")}
                          </p>
                        </div>
                      </TableCell>
                    </TableRow>
                  )
                : filtered.map((row) => (
                    <PublicationRow
                      key={row.id}
                      row={row}
                      isAdmin={isAdmin}
                      isPolling={pollingId === row.id}
                      onCheckDrift={handleCheckDrift}
                      onReconcile={handleReconcileClick}
                    />
                  ))}
          </TableBody>
        </Table>
      </div>

      <ReconcilePublicationDialog
        key={selectedPublication?.id ?? "new"}
        open={reconcileOpen}
        onOpenChange={handleReconcileOpenChange}
        publication={selectedPublication}
      />
    </div>
  );
}

interface PublicationRowProps {
  row: PublicationListItem;
  isAdmin: boolean;
  isPolling: boolean;
  onCheckDrift: (publication: PublicationListItem) => void;
  onReconcile: (publication: PublicationListItem) => void;
}

function PublicationRow({
  row,
  isAdmin,
  isPolling,
  onCheckDrift,
  onReconcile,
}: PublicationRowProps) {
  const { t } = useTranslation();

  return (
    <TableRow>
      <TableCell className="font-medium">{row.tenantName}</TableCell>
      <TableCell>{row.infobaseName}</TableCell>
      <TableCell>{row.siteName}</TableCell>
      <TableCell className="font-mono text-xs">{row.virtualPath}</TableCell>
      <TableCell className="font-mono text-xs">{row.platformVersion}</TableCell>
      <TableCell>
        <StatusBadge variant={row.enableOData ? "success" : "neutral"}>
          {row.enableOData ? t("publications.badges.enabled") : t("publications.badges.disabled")}
        </StatusBadge>
      </TableCell>
      <TableCell>
        <StatusBadge variant={row.enableHttpServices ? "success" : "neutral"}>
          {row.enableHttpServices
            ? t("publications.badges.enabled")
            : t("publications.badges.disabled")}
        </StatusBadge>
      </TableCell>
      <TableCell>
        <StatusBadge variant={DRIFT_VARIANT[row.lastDriftStatus]}>
          {t(`publications.driftStatus.${row.lastDriftStatus.toLowerCase()}`)}
        </StatusBadge>
      </TableCell>
      <TableCell>
        {row.lastDriftCheckAt ? (
          <RelativeTime value={row.lastDriftCheckAt} />
        ) : (
          <span className="text-muted-foreground">{t("publications.driftStatus.unknown")}</span>
        )}
      </TableCell>
      {isAdmin && (
        <TableCell className="text-right">
          <div className="flex justify-end gap-2">
            <Button
              size="sm"
              variant="outline"
              disabled={isPolling}
              onClick={() => onCheckDrift(row)}
            >
              {isPolling ? (
                <Loader2Icon className="size-4 animate-spin" />
              ) : (
                t("publications.actions.checkDrift")
              )}
            </Button>
            <Button size="sm" variant="outline" onClick={() => onReconcile(row)}>
              {t("publications.actions.reconcile")}
            </Button>
          </div>
        </TableCell>
      )}
    </TableRow>
  );
}
