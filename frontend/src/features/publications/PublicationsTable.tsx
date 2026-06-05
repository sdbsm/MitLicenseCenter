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
import type { PublicationListItem, PublicationPublishStatus, PublicationSource } from "./types";

const STATUS_VARIANT: Record<PublicationPublishStatus, StatusBadgeVariant> = {
  Published: "success",
  NotPublished: "info",
  Error: "danger",
  Unknown: "neutral",
};

const SOURCE_VARIANT: Record<PublicationSource, StatusBadgeVariant> = {
  Webinst: "success",
  Configurator: "info",
  Unknown: "neutral",
};

interface PublicationsTableProps {
  rows: PublicationListItem[];
  isLoading: boolean;
  isError: boolean;
  isAdmin: boolean;
  hasAnyPublications: boolean;
  checkingId: string | null;
  onCheck: (publication: PublicationListItem) => void;
  onPublish: (publication: PublicationListItem) => void;
  onChangePlatform: (publication: PublicationListItem) => void;
}

/** Таблица публикаций: шапка, скелет загрузки, пустое состояние и строки. */
export function PublicationsTable({
  rows,
  isLoading,
  isError,
  isAdmin,
  hasAnyPublications,
  checkingId,
  onCheck,
  onPublish,
  onChangePlatform,
}: PublicationsTableProps) {
  const { t } = useTranslation();
  const columnCount = isAdmin ? 9 : 8;

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
            <TableHead className="w-32">{t("publications.table.headers.source")}</TableHead>
            <TableHead className="w-36">{t("publications.table.headers.status")}</TableHead>
            <TableHead className="w-36">{t("publications.table.headers.lastChecked")}</TableHead>
            {isAdmin && (
              <TableHead className="w-72 text-right">
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
                    isChecking={checkingId === row.id}
                    onCheck={onCheck}
                    onPublish={onPublish}
                    onChangePlatform={onChangePlatform}
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
  isChecking: boolean;
  onCheck: (publication: PublicationListItem) => void;
  onPublish: (publication: PublicationListItem) => void;
  onChangePlatform: (publication: PublicationListItem) => void;
}

function PublicationRow({
  row,
  isAdmin,
  isChecking,
  onCheck,
  onPublish,
  onChangePlatform,
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
        <StatusBadge variant={SOURCE_VARIANT[row.source]}>
          {t(`publications.source.${row.source.toLowerCase()}`)}
        </StatusBadge>
      </TableCell>
      <TableCell>
        {row.lastCheckDetails ? (
          <Tooltip>
            <TooltipTrigger asChild>
              <span className="cursor-help">
                <StatusBadge variant={STATUS_VARIANT[row.lastCheckStatus]}>
                  {t(`publications.status.${row.lastCheckStatus.toLowerCase()}`)}
                </StatusBadge>
              </span>
            </TooltipTrigger>
            <TooltipContent className="max-w-sm whitespace-pre-line">
              {row.lastCheckDetails}
            </TooltipContent>
          </Tooltip>
        ) : (
          <StatusBadge variant={STATUS_VARIANT[row.lastCheckStatus]}>
            {t(`publications.status.${row.lastCheckStatus.toLowerCase()}`)}
          </StatusBadge>
        )}
      </TableCell>
      <TableCell>
        {row.lastCheckAt ? (
          <RelativeTime value={row.lastCheckAt} />
        ) : (
          <span className="text-muted-foreground">{t("publications.status.unknown")}</span>
        )}
      </TableCell>
      {isAdmin && (
        <TableCell className="text-right">
          <div className="flex justify-end gap-2">
            <Button size="sm" variant="outline" disabled={isChecking} onClick={() => onCheck(row)}>
              {isChecking ? (
                <Loader2Icon className="size-4 animate-spin" />
              ) : (
                t("publications.actions.check")
              )}
            </Button>
            <Button size="sm" variant="outline" onClick={() => onPublish(row)}>
              {t("publications.actions.publish")}
            </Button>
            <Button size="sm" variant="outline" onClick={() => onChangePlatform(row)}>
              {t("publications.actions.changePlatform")}
            </Button>
          </div>
        </TableCell>
      )}
    </TableRow>
  );
}
