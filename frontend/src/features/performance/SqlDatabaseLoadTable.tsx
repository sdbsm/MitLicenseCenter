import { useMemo } from "react";
import { useTranslation } from "react-i18next";
import { StatusBadge } from "@/components/ui/StatusBadge";
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from "@/components/ui/table";
import { formatMs } from "./onecLoad";
import { aggregateByDatabase } from "./sqlLoad";
import type { SqlActiveRequest, SqlDatabaseAttribution } from "./types";

interface SqlDatabaseLoadTableProps {
  requests: SqlActiveRequest[];
  attributionMap: Map<string, SqlDatabaseAttribution>;
}

/**
 * «Нагрузка по базам/клиентам» (MLC-069) — джойн активных запросов с атрибуцией база→клиент:
 * суммарное ЦП-время и число запросов по базе, плюс сколько из них 1С-originated. Это разрез
 * «какая база/клиент грузит SQL»; гранулярность — база (SQL→сеанс→юзер невозможна, ADR-26).
 */
export function SqlDatabaseLoadTable({ requests, attributionMap }: SqlDatabaseLoadTableProps) {
  const { t } = useTranslation();
  const rows = useMemo(
    () => aggregateByDatabase(requests, attributionMap),
    [requests, attributionMap]
  );

  return (
    <div className="rounded-md border">
      <Table>
        <TableHeader>
          <TableRow>
            <TableHead>{t("performance.sql.byDatabase.database")}</TableHead>
            <TableHead>{t("performance.sql.byDatabase.client")}</TableHead>
            <TableHead className="w-32 text-right">
              {t("performance.sql.byDatabase.requests")}
            </TableHead>
            <TableHead className="w-24 text-right">{t("performance.sql.byDatabase.cpu")}</TableHead>
          </TableRow>
        </TableHeader>
        <TableBody>
          {rows.length === 0 ? (
            <TableRow>
              <TableCell colSpan={4} className="text-muted-foreground py-8 text-center text-sm">
                {t("performance.sql.byDatabase.empty")}
              </TableCell>
            </TableRow>
          ) : (
            rows.map((row) => (
              <TableRow key={row.databaseName}>
                <TableCell className="font-medium">{row.databaseName}</TableCell>
                <TableCell className="text-muted-foreground text-sm">
                  {row.attribution?.tenantName
                    ? `${row.attribution.tenantName}${row.attribution.infobaseName ? ` · ${row.attribution.infobaseName}` : ""}`
                    : "—"}
                </TableCell>
                <TableCell className="text-right tabular-nums">
                  <div className="flex items-center justify-end gap-2">
                    <span>{row.requestCount}</span>
                    {row.oneCRequestCount > 0 && (
                      <StatusBadge variant="info">
                        {t("performance.sql.byDatabase.oneC", { count: row.oneCRequestCount })}
                      </StatusBadge>
                    )}
                  </div>
                </TableCell>
                <TableCell className="text-right tabular-nums">{formatMs(row.cpuTimeMs)}</TableCell>
              </TableRow>
            ))
          )}
        </TableBody>
      </Table>
    </div>
  );
}
