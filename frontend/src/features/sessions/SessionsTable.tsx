import { format } from "date-fns";
import { ru } from "date-fns/locale";
import { ArrowDownIcon, ArrowUpIcon, ArrowUpDownIcon, MonitorPlayIcon } from "lucide-react";
import { useTranslation } from "react-i18next";
import { StatusBadge } from "@/components/ui/StatusBadge";
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
import type { SessionSort, SessionSortKey } from "./useSessionsPage";
import type { SessionSnapshotEntry } from "./types";

function formatDuration(seconds: number): string {
  if (seconds < 60) {
    return `${seconds}с`;
  }
  const minutes = Math.floor(seconds / 60);
  if (minutes < 60) {
    const secs = seconds % 60;
    return secs > 0 ? `${minutes}м ${secs}с` : `${minutes}м`;
  }
  const hours = Math.floor(minutes / 60);
  const mins = minutes % 60;
  return mins > 0 ? `${hours}ч ${mins}м` : `${hours}ч`;
}

interface SortableHeadProps {
  sortKey: SessionSortKey;
  sort: SessionSort;
  onToggle: (key: SessionSortKey) => void;
  children: React.ReactNode;
  className?: string;
}

function SortableHead({ sortKey, sort, onToggle, children, className }: SortableHeadProps) {
  const isActive = sort.key === sortKey;
  return (
    <TableHead className={className}>
      <button
        type="button"
        onClick={() => onToggle(sortKey)}
        className="hover:text-foreground flex items-center gap-1 transition-colors select-none"
      >
        {children}
        {isActive ? (
          sort.dir === "asc" ? (
            <ArrowUpIcon className="size-3.5 shrink-0" />
          ) : (
            <ArrowDownIcon className="size-3.5 shrink-0" />
          )
        ) : (
          <ArrowUpDownIcon className="size-3.5 shrink-0 opacity-40" />
        )}
      </button>
    </TableHead>
  );
}

interface SessionsTableProps {
  rows: SessionSnapshotEntry[];
  isLoading: boolean;
  isError: boolean;
  isAdmin: boolean;
  sort: SessionSort;
  onToggleSort: (key: SessionSortKey) => void;
  onKill: (session: SessionSnapshotEntry) => void;
}

/** Таблица сессий: шапка с сортировкой по колонкам, скелет загрузки, пустое состояние и строки. */
export function SessionsTable({
  rows,
  isLoading,
  isError,
  isAdmin,
  sort,
  onToggleSort,
  onKill,
}: SessionsTableProps) {
  const { t } = useTranslation();
  return (
    <div className="rounded-md border">
      <Table>
        <TableHeader>
          <TableRow>
            <SortableHead sortKey="tenantName" sort={sort} onToggle={onToggleSort}>
              {t("sessions.table.tenant")}
            </SortableHead>
            <SortableHead sortKey="infobaseName" sort={sort} onToggle={onToggleSort}>
              {t("sessions.table.infobase")}
            </SortableHead>
            <TableHead className="w-28">{t("sessions.table.sessionId")}</TableHead>
            <TableHead className="w-28">{t("sessions.table.appId")}</TableHead>
            <SortableHead sortKey="userName" sort={sort} onToggle={onToggleSort}>
              {t("sessions.table.user")}
            </SortableHead>
            <SortableHead sortKey="startedAt" sort={sort} onToggle={onToggleSort} className="w-40">
              {t("sessions.table.startedAt")}
            </SortableHead>
            <SortableHead
              sortKey="durationSeconds"
              sort={sort}
              onToggle={onToggleSort}
              className="w-24"
            >
              {t("sessions.table.duration")}
            </SortableHead>
            <SortableHead
              sortKey="consumesLicense"
              sort={sort}
              onToggle={onToggleSort}
              className="w-28"
            >
              {t("sessions.table.consumesLicense")}
            </SortableHead>
            {isAdmin && <TableHead className="w-36">{t("sessions.table.action")}</TableHead>}
          </TableRow>
        </TableHeader>
        <TableBody>
          {isLoading
            ? Array.from({ length: 5 }).map((_, idx) => (
                <TableRow key={`skeleton-${idx}`}>
                  {Array.from({ length: isAdmin ? 9 : 8 }).map((__, col) => (
                    <TableCell key={col}>
                      <Skeleton className="h-4 w-full" />
                    </TableCell>
                  ))}
                </TableRow>
              ))
            : rows.length === 0
              ? !isError && (
                  <TableRow>
                    <TableCell colSpan={isAdmin ? 9 : 8} className="py-12">
                      <div className="flex flex-col items-center justify-center gap-3 text-center">
                        <MonitorPlayIcon className="text-muted-foreground size-8" />
                        <div className="space-y-1">
                          <p className="font-medium">{t("sessions.empty.title")}</p>
                          <p className="text-muted-foreground text-sm">
                            {t("sessions.empty.hint")}
                          </p>
                        </div>
                      </div>
                    </TableCell>
                  </TableRow>
                )
              : rows.map((row) => (
                  <SessionRow key={row.sessionId} row={row} isAdmin={isAdmin} onKill={onKill} />
                ))}
        </TableBody>
      </Table>
    </div>
  );
}

interface SessionRowProps {
  row: SessionSnapshotEntry;
  isAdmin: boolean;
  onKill: (session: SessionSnapshotEntry) => void;
}

function SessionRow({ row, isAdmin, onKill }: SessionRowProps) {
  const { t } = useTranslation();
  const startedAtFormatted = format(new Date(row.startedAt), "dd.MM.yyyy HH:mm:ss", {
    locale: ru,
  });

  return (
    <TableRow>
      <TableCell className="font-medium">{row.tenantName}</TableCell>
      <TableCell>{row.infobaseName}</TableCell>
      <TableCell>
        <Tooltip>
          <TooltipTrigger asChild>
            <span className="cursor-help font-mono text-xs">
              {row.sessionId.replace(/-/g, "").slice(0, 8)}
            </span>
          </TooltipTrigger>
          <TooltipContent>
            <span className="font-mono text-xs">{row.sessionId}</span>
          </TooltipContent>
        </Tooltip>
      </TableCell>
      <TableCell className="font-mono text-xs">{row.appId}</TableCell>
      <TableCell>
        {row.userName.trim() ? (
          row.userName
        ) : (
          <span className="text-muted-foreground italic">{t("sessions.noUser")}</span>
        )}
      </TableCell>
      <TableCell className="text-sm tabular-nums">{startedAtFormatted}</TableCell>
      <TableCell className="text-sm tabular-nums">{formatDuration(row.durationSeconds)}</TableCell>
      <TableCell>
        <StatusBadge variant={row.consumesLicense ? "success" : "neutral"}>
          {row.consumesLicense ? t("sessions.badges.consumesYes") : t("sessions.badges.consumesNo")}
        </StatusBadge>
      </TableCell>
      {isAdmin && (
        <TableCell>
          <Button size="sm" variant="outline" onClick={() => onKill(row)}>
            {t("sessions.kill.confirm")}
          </Button>
        </TableCell>
      )}
    </TableRow>
  );
}
