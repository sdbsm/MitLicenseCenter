import { UsersIcon } from "lucide-react";
import { useTranslation } from "react-i18next";
import { StatusBadge } from "@/components/ui/StatusBadge";
import { Progress } from "@/components/ui/progress";
import { Skeleton } from "@/components/ui/skeleton";
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from "@/components/ui/table";
import { quotaDisplay } from "@/lib/quota";
import type { ByTenantRow } from "./byTenantRows";

interface ByTenantTableProps {
  rows: ByTenantRow[];
  isLoading: boolean;
  isError: boolean;
  /** Клик по строке клиента → «Живые сеансы» с фильтром по этому клиенту. */
  onRowClick: (row: ByTenantRow) => void;
}

/**
 * Проекция «По клиентам» (MLC-196a) — таблица-агрегат «кто сколько потребляет».
 * Колонки: Клиент · Потребляет · Лимит · Загрузка (полоса + %). Цвет/ярлык/полоса —
 * строго через `lib/quota.ts`; статусный ярлык квоты — через `StatusBadge`
 * (`common.quota.*`). Клик по строке переключает на «Живые сеансы» с фильтром клиента.
 * Сортировка строк (превышения сверху, затем consumed ↓) — в `buildByTenantRows`.
 */
export function ByTenantTable({ rows, isLoading, isError, onRowClick }: ByTenantTableProps) {
  const { t } = useTranslation();

  if (isError) return null;

  return (
    <div className="overflow-hidden rounded-md border">
      <Table>
        <TableHeader>
          <TableRow>
            <TableHead>{t("sessions.byTenant.columns.tenant")}</TableHead>
            <TableHead className="text-right">{t("sessions.byTenant.columns.consumed")}</TableHead>
            <TableHead className="text-right">{t("sessions.byTenant.columns.limit")}</TableHead>
            <TableHead className="w-[40%]">{t("sessions.byTenant.columns.load")}</TableHead>
          </TableRow>
        </TableHeader>
        <TableBody>
          {isLoading ? (
            Array.from({ length: 5 }).map((_, i) => (
              <TableRow key={i}>
                {Array.from({ length: 4 }).map((__, c) => (
                  <TableCell key={c}>
                    <Skeleton className="h-4 w-full" />
                  </TableCell>
                ))}
              </TableRow>
            ))
          ) : rows.length === 0 ? (
            <TableRow>
              <TableCell colSpan={4}>
                <div className="flex flex-col items-center justify-center gap-2 py-10 text-center">
                  <UsersIcon className="text-muted-foreground size-8" aria-hidden="true" />
                  <p className="text-sm font-medium">{t("sessions.byTenant.empty.title")}</p>
                  <p className="text-muted-foreground text-xs">
                    {t("sessions.byTenant.empty.hint")}
                  </p>
                </div>
              </TableCell>
            </TableRow>
          ) : (
            rows.map((row) => (
              <ByTenantRowCells key={row.tenantId} row={row} onClick={onRowClick} />
            ))
          )}
        </TableBody>
      </Table>
    </div>
  );
}

function ByTenantRowCells({
  row,
  onClick,
}: {
  row: ByTenantRow;
  onClick: (row: ByTenantRow) => void;
}) {
  const { t } = useTranslation();
  const quota = quotaDisplay(row.consumed, row.limit);
  const unlimited = row.limit <= 0;

  return (
    <TableRow
      className="hover:bg-muted/50 cursor-pointer"
      role="button"
      tabIndex={0}
      onClick={() => onClick(row)}
      onKeyDown={(e) => {
        if (e.key === "Enter" || e.key === " ") {
          e.preventDefault();
          onClick(row);
        }
      }}
    >
      <TableCell className="font-medium">{row.tenantName}</TableCell>
      <TableCell className="text-right font-mono tabular-nums">{row.consumed}</TableCell>
      <TableCell className="text-muted-foreground text-right font-mono tabular-nums">
        {unlimited ? t("sessions.byTenant.unlimited") : row.limit}
      </TableCell>
      <TableCell>
        {unlimited ? (
          <span className="text-muted-foreground text-sm">{t("sessions.byTenant.unlimited")}</span>
        ) : (
          <div className="flex items-center gap-3">
            <Progress value={Math.min(quota.percent, 100)} className={quota.progressClass} />
            <span className="w-10 shrink-0 text-right font-mono text-sm tabular-nums">
              {quota.percent}%
            </span>
            {quota.label && (
              <StatusBadge variant={quota.badgeVariant} className="shrink-0">
                {t(`common.quota.${quota.label}`)}
              </StatusBadge>
            )}
          </div>
        )}
      </TableCell>
    </TableRow>
  );
}
