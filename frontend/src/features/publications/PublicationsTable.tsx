import { Loader2Icon, ServerIcon } from "lucide-react";
import { useTranslation } from "react-i18next";
import { RelativeTime } from "@/components/ui/RelativeTime";
import { StatusBadge, type StatusBadgeVariant } from "@/components/ui/StatusBadge";
import { Button } from "@/components/ui/button";
import { Skeleton } from "@/components/ui/skeleton";
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from "@/components/ui/table";
import { Tooltip, TooltipContent, TooltipTrigger } from "@/components/ui/tooltip";
import type { PublicationDriftStatus, PublicationListItem } from "./types";

const DRIFT_VARIANT: Record<PublicationDriftStatus, StatusBadgeVariant> = {
  InSync: "success",
  Drift: "danger",
  Missing: "info",
  Error: "danger",
};

interface PublicationsTableProps {
  rows: PublicationListItem[];
  isLoading: boolean;
  isError: boolean;
  isAdmin: boolean;
  hasAnyPublications: boolean;
  pollingId: string | null;
  onCheckDrift: (publication: PublicationListItem) => void;
  onReconcile: (publication: PublicationListItem) => void;
}

/** Таблица публикаций: шапка, скелет загрузки, пустое состояние и строки. */
export function PublicationsTable({
  rows,
  isLoading,
  isError,
  isAdmin,
  hasAnyPublications,
  pollingId,
  onCheckDrift,
  onReconcile,
}: PublicationsTableProps) {
  const { t } = useTranslation();
  const columnCount = isAdmin ? 10 : 9;

  return (
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
            : rows.length === 0
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
              : rows.map((row) => (
                  <PublicationRow
                    key={row.id}
                    row={row}
                    isAdmin={isAdmin}
                    isPolling={pollingId === row.id}
                    onCheckDrift={onCheckDrift}
                    onReconcile={onReconcile}
                  />
                ))}
        </TableBody>
      </Table>
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
        {row.lastDriftDetails ? (
          <Tooltip>
            <TooltipTrigger asChild>
              <span className="cursor-help">
                <StatusBadge variant={DRIFT_VARIANT[row.lastDriftStatus]}>
                  {t(`publications.driftStatus.${row.lastDriftStatus.toLowerCase()}`)}
                </StatusBadge>
              </span>
            </TooltipTrigger>
            <TooltipContent className="max-w-sm whitespace-pre-line">
              {row.lastDriftDetails}
            </TooltipContent>
          </Tooltip>
        ) : (
          <StatusBadge variant={DRIFT_VARIANT[row.lastDriftStatus]}>
            {t(`publications.driftStatus.${row.lastDriftStatus.toLowerCase()}`)}
          </StatusBadge>
        )}
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
