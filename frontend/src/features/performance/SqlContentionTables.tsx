import { useTranslation } from "react-i18next";
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from "@/components/ui/table";
import { formatMs } from "./onecLoad";
import { formatInt } from "./sqlLoad";
import type { SqlDatabaseIo, SqlWaitDelta } from "./types";

/**
 * Конкуренция за ресурсы SQL за интервал между двумя poll'ами (MLC-069): дельта wait-stats
 * (на чём ждёт сервер) и IO-stall по базам (кто упирается в диск). Обе метрики кумулятивны
 * с старта SQL → значимы только как дельта; на первом poll'е (`measuring`) родитель показывает
 * «измеряю…» вместо этих таблиц.
 */
export function SqlWaitsTable({ waits }: { waits: SqlWaitDelta[] }) {
  const { t } = useTranslation();

  return (
    <div className="rounded-md border">
      <Table>
        <TableHeader>
          <TableRow>
            <TableHead>{t("performance.sql.waits.type")}</TableHead>
            <TableHead className="w-28 text-right">{t("performance.sql.waits.time")}</TableHead>
            <TableHead className="w-24 text-right">{t("performance.sql.waits.tasks")}</TableHead>
          </TableRow>
        </TableHeader>
        <TableBody>
          {waits.length === 0 ? (
            <TableRow>
              <TableCell colSpan={3} className="text-muted-foreground py-6 text-center text-sm">
                {t("performance.sql.waits.empty")}
              </TableCell>
            </TableRow>
          ) : (
            waits.map((w) => (
              <TableRow key={w.waitType}>
                <TableCell className="font-mono text-xs">{w.waitType}</TableCell>
                <TableCell className="text-right tabular-nums">
                  {formatMs(w.waitTimeMsDelta)}
                </TableCell>
                <TableCell className="text-muted-foreground text-right tabular-nums">
                  {formatInt(w.waitingTasksDelta)}
                </TableCell>
              </TableRow>
            ))
          )}
        </TableBody>
      </Table>
    </div>
  );
}

export function SqlDatabaseIoTable({ io }: { io: SqlDatabaseIo[] }) {
  const { t } = useTranslation();

  return (
    <div className="rounded-md border">
      <Table>
        <TableHeader>
          <TableRow>
            <TableHead>{t("performance.sql.io.database")}</TableHead>
            <TableHead className="w-28 text-right">{t("performance.sql.io.readStall")}</TableHead>
            <TableHead className="w-28 text-right">{t("performance.sql.io.writeStall")}</TableHead>
            <TableHead className="w-24 text-right">{t("performance.sql.io.ops")}</TableHead>
          </TableRow>
        </TableHeader>
        <TableBody>
          {io.length === 0 ? (
            <TableRow>
              <TableCell colSpan={4} className="text-muted-foreground py-6 text-center text-sm">
                {t("performance.sql.io.empty")}
              </TableCell>
            </TableRow>
          ) : (
            io.map((row, i) => (
              <TableRow key={row.databaseName ?? `__db_${i}`}>
                <TableCell className="font-medium">{row.databaseName ?? "—"}</TableCell>
                <TableCell className="text-right tabular-nums">
                  {formatMs(row.readStallMsDelta)}
                </TableCell>
                <TableCell className="text-right tabular-nums">
                  {formatMs(row.writeStallMsDelta)}
                </TableCell>
                <TableCell className="text-muted-foreground text-right tabular-nums">
                  {formatInt(row.readsDelta + row.writesDelta)}
                </TableCell>
              </TableRow>
            ))
          )}
        </TableBody>
      </Table>
    </div>
  );
}
