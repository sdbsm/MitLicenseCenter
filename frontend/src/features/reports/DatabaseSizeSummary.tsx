import { format } from "date-fns";
import { ru } from "date-fns/locale";
import { useTranslation } from "react-i18next";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Skeleton } from "@/components/ui/skeleton";
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from "@/components/ui/table";
import { formatBytes } from "@/lib/formatBytes";
import { DatabaseSizeChart } from "./DatabaseSizeChart";
import { ReportsEmptyState } from "./ReportsEmptyState";
import type { DatabaseSizeSeriesResponse, DatabaseSizeTenantRow } from "./types";

interface DatabaseSizeSummaryProps {
  data: DatabaseSizeSeriesResponse | undefined;
  isLoading: boolean;
}

// Стабильный ключ строки таблицы: реальный tenantId либо синтетический для «без
// клиента» (null tenant — единственная такая строка на снимок).
function rowKey(row: DatabaseSizeTenantRow): string {
  return row.tenantId ?? "__no_tenant__";
}

/** Секция сводки размера баз по хосту: эффективный период, график роста итога во
 *  времени и таблица разбивки по клиентам на последний снимок (бэкенд сортирует по
 *  убыванию размера). Экспорт — 185g, здесь нет. */
export function DatabaseSizeSummary({ data, isLoading }: DatabaseSizeSummaryProps) {
  const { t } = useTranslation();
  const isEmpty = !data || data.points.length === 0;

  return (
    <Card>
      <CardHeader className="gap-2">
        <CardTitle>{t("reports.size.summary.title")}</CardTitle>
        {data && (
          <p className="text-muted-foreground text-sm">
            {t("reports.summary.effectiveRange", {
              from: format(new Date(data.fromUtc), "dd.MM.yyyy HH:mm", { locale: ru }),
              to: format(new Date(data.toUtc), "dd.MM.yyyy HH:mm", { locale: ru }),
            })}{" "}
            <span className="text-xs">({t("reports.summary.timezoneHint")})</span>
          </p>
        )}
      </CardHeader>
      <CardContent className="space-y-4">
        {isLoading && !data ? (
          <Skeleton className="h-[320px] w-full" />
        ) : isEmpty ? (
          <ReportsEmptyState />
        ) : (
          <>
            <DatabaseSizeChart points={data.points} isLoading={isLoading} />

            {data.tenants.length > 0 && (
              <div className="space-y-2">
                <h3 className="text-sm font-medium">{t("reports.size.tenants.title")}</h3>
                <Table>
                  <TableHeader>
                    <TableRow>
                      <TableHead>{t("reports.size.columns.tenant")}</TableHead>
                      <TableHead className="text-right">
                        {t("reports.size.columns.total")}
                      </TableHead>
                      <TableHead className="text-right">{t("reports.size.columns.data")}</TableHead>
                      <TableHead className="text-right">{t("reports.size.columns.log")}</TableHead>
                      <TableHead className="text-right">
                        {t("reports.size.columns.databaseCount")}
                      </TableHead>
                    </TableRow>
                  </TableHeader>
                  <TableBody>
                    {data.tenants.map((row) => (
                      <TableRow key={rowKey(row)}>
                        <TableCell className="font-medium">
                          {row.tenantName ?? (
                            <span className="text-muted-foreground italic">
                              {t("reports.size.noTenant")}
                            </span>
                          )}
                        </TableCell>
                        <TableCell className="text-right font-medium tabular-nums">
                          {formatBytes(row.totalBytes)}
                        </TableCell>
                        <TableCell className="text-muted-foreground text-right tabular-nums">
                          {formatBytes(row.dataBytes)}
                        </TableCell>
                        <TableCell className="text-muted-foreground text-right tabular-nums">
                          {formatBytes(row.logBytes)}
                        </TableCell>
                        <TableCell className="text-right tabular-nums">
                          {row.databaseCount}
                        </TableCell>
                      </TableRow>
                    ))}
                  </TableBody>
                </Table>
              </div>
            )}
          </>
        )}
      </CardContent>
    </Card>
  );
}
