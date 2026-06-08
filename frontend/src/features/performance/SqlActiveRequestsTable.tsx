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
import { Tooltip, TooltipContent, TooltipTrigger } from "@/components/ui/tooltip";
import { formatMs } from "./onecLoad";
import { collectBlockerIds, formatInt, sortRequestsByCpu } from "./sqlLoad";
import type { SqlActiveRequest, SqlDatabaseAttribution } from "./types";

interface SqlActiveRequestsTableProps {
  requests: SqlActiveRequest[];
  attributionMap: Map<string, SqlDatabaseAttribution>;
}

/**
 * Активные запросы MSSQL «кто грузит сейчас» (MLC-069) — топ по ЦП-времени. Бейдж 1С
 * (`program_name='1CV83 Server'`) отделяет 1С-originated SQL от стороннего; бейдж блокировки
 * (`ждёт сеанс N` / `блокирует`) делает видимой цепочку блокировок. Текст запроса обрезан,
 * полный — в тултипе. Отсутствующие perf-поля — «—», не 0.
 */
export function SqlActiveRequestsTable({ requests, attributionMap }: SqlActiveRequestsTableProps) {
  const { t } = useTranslation();
  const rows = useMemo(() => sortRequestsByCpu(requests), [requests]);
  const blockerIds = useMemo(() => collectBlockerIds(requests), [requests]);

  return (
    <div className="rounded-md border">
      <Table>
        <TableHeader>
          <TableRow>
            <TableHead className="w-40">{t("performance.sql.requests.session")}</TableHead>
            <TableHead>{t("performance.sql.requests.client")}</TableHead>
            <TableHead className="w-24 text-right">{t("performance.sql.requests.cpu")}</TableHead>
            <TableHead className="w-24 text-right">
              {t("performance.sql.requests.elapsed")}
            </TableHead>
            <TableHead className="w-24 text-right">{t("performance.sql.requests.reads")}</TableHead>
            <TableHead className="w-32">{t("performance.sql.requests.wait")}</TableHead>
            <TableHead>{t("performance.sql.requests.text")}</TableHead>
          </TableRow>
        </TableHeader>
        <TableBody>
          {rows.length === 0 ? (
            <TableRow>
              <TableCell colSpan={7} className="text-muted-foreground py-8 text-center text-sm">
                {t("performance.sql.requests.empty")}
              </TableCell>
            </TableRow>
          ) : (
            rows.map((r) => (
              <RequestRow
                key={r.sessionId}
                request={r}
                blocks={blockerIds.has(r.sessionId)}
                attributionMap={attributionMap}
              />
            ))
          )}
        </TableBody>
      </Table>
    </div>
  );
}

function RequestRow({
  request,
  blocks,
  attributionMap,
}: {
  request: SqlActiveRequest;
  blocks: boolean;
  attributionMap: Map<string, SqlDatabaseAttribution>;
}) {
  const { t } = useTranslation();
  const attribution = request.databaseName
    ? (attributionMap.get(request.databaseName.trim().toLowerCase()) ?? null)
    : null;

  return (
    <TableRow>
      <TableCell>
        <div className="flex flex-wrap items-center gap-1">
          <span className="font-mono text-xs tabular-nums">{request.sessionId}</span>
          {request.isOneC && (
            <StatusBadge variant="info">{t("performance.sql.oneCBadge")}</StatusBadge>
          )}
          {request.blockingSessionId !== null && (
            <StatusBadge variant="danger">
              {t("performance.sql.requests.blockedBy", { id: request.blockingSessionId })}
            </StatusBadge>
          )}
          {blocks && (
            <StatusBadge variant="warning">{t("performance.sql.requests.blocks")}</StatusBadge>
          )}
        </div>
      </TableCell>
      <TableCell>
        <ClientCell databaseName={request.databaseName} attribution={attribution} />
      </TableCell>
      <TableCell className="text-right tabular-nums">{formatMs(request.cpuTimeMs)}</TableCell>
      <TableCell className="text-muted-foreground text-right tabular-nums">
        {formatMs(request.elapsedMs)}
      </TableCell>
      <TableCell className="text-muted-foreground text-right tabular-nums">
        {formatInt(request.logicalReads)}
      </TableCell>
      <TableCell className="text-muted-foreground text-sm">
        {request.waitType ? (
          <span title={`${request.waitType} · ${formatMs(request.waitTimeMs)}`}>
            {request.waitType}
          </span>
        ) : (
          "—"
        )}
      </TableCell>
      <TableCell className="max-w-md">
        {request.sqlText ? (
          <Tooltip>
            <TooltipTrigger asChild>
              <span className="block cursor-help truncate font-mono text-xs">
                {request.sqlText}
              </span>
            </TooltipTrigger>
            <TooltipContent className="max-w-xl">
              <span className="font-mono text-xs whitespace-pre-wrap">{request.sqlText}</span>
            </TooltipContent>
          </Tooltip>
        ) : (
          <span className="text-muted-foreground text-sm italic">
            {t("performance.sql.noText")}
          </span>
        )}
      </TableCell>
    </TableRow>
  );
}

// Клиент базы: имя базы + клиент (tenant · infobase) либо «—», когда базе не соответствует
// зарегистрированная инфобаза (системная/незарегистрированная — видна, но «ничья»).
export function ClientCell({
  databaseName,
  attribution,
}: {
  databaseName: string | null;
  attribution: SqlDatabaseAttribution | null;
}) {
  return (
    <div className="space-y-0.5">
      <div className="font-medium">{databaseName ?? "—"}</div>
      <div className="text-muted-foreground text-xs">
        {attribution?.tenantName
          ? `${attribution.tenantName}${attribution.infobaseName ? ` · ${attribution.infobaseName}` : ""}`
          : "—"}
      </div>
    </div>
  );
}
