import { format, formatDistanceToNow } from "date-fns";
import { ru } from "date-fns/locale";
import { ScrollTextIcon } from "lucide-react";
import { useTranslation } from "react-i18next";
import { Link } from "react-router";
import { Badge } from "@/components/ui/badge";
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
import { type AuditActionType, type AuditEntry } from "./types";

interface AuditTableProps {
  items: AuditEntry[];
  isLoading: boolean;
  isError: boolean;
  tenantNameById: Map<string, string>;
}

/** Таблица журнала аудита: шапка, скелет загрузки, пустое состояние и строки. */
export function AuditTable({ items, isLoading, isError, tenantNameById }: AuditTableProps) {
  const { t } = useTranslation();
  return (
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
                          <p className="text-muted-foreground text-sm">{t("audit.empty.hint")}</p>
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
                      entry.tenantId ? (tenantNameById.get(entry.tenantId) ?? null) : null
                    }
                  />
                ))}
        </TableBody>
      </Table>
    </div>
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
            <span className="cursor-help tabular-nums">{exact}</span>
          </TooltipTrigger>
          <TooltipContent>
            <span>{relative}</span>
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
