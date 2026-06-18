import { useMemo } from "react";
import { LockIcon } from "lucide-react";
import { useTranslation } from "react-i18next";
import { StatusBadge } from "@/components/ui/StatusBadge";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from "@/components/ui/table";
import { ClientCell } from "./SqlActiveRequestsTable";
import { blockedSessions, formatMs } from "./onecLoad";
import {
  attributionFor,
  buildAttributionMap,
  lockChainRows,
  type SqlLockChainRow,
} from "./sqlLoad";
import type { OneCSessionLoad } from "./types";
import { useOneCLoad } from "./useOneCLoad";
import { useSqlPerformance } from "./useSqlPerformance";

/**
 * Единый блок «Блокировки» (MLC-210, Фаза 3 «Быстродействие», Срез D) — сводит в одно место два
 * сигнала контеншена, иначе разнесённые по слоям drill-down: цепочки блокировок SQL
 * (`blockingSessionId`) и заблокированные сеансы 1С (`blockedByDbms`/`blockedByLs`). Читает оба
 * live-источника повторно (`useOneCLoad`/`useSqlPerformance`) — React Query дедуплицирует по ключу,
 * двойного polling нет. Пусто (никто никого не ждёт) — нейтральное «блокировок нет», это хороший
 * случай: красных акцентов нет. Акценты только через StatusBadge (палитра монохром).
 */
export function PerformanceBlockingBlock({ paused }: { paused: boolean }) {
  const { t } = useTranslation();
  const onec = useOneCLoad(paused);
  const sql = useSqlPerformance(paused);

  const oneCBlocked = blockedSessions(onec.data?.sessions ?? []);
  const requests = sql.data?.snapshot?.status === "Ok" ? sql.data.snapshot.activeRequests : [];
  const sqlChains = lockChainRows(requests);
  const attributionMap = useMemo(
    () => buildAttributionMap(sql.data?.databases ?? []),
    [sql.data?.databases]
  );

  const isEmpty = oneCBlocked.length === 0 && sqlChains.length === 0;

  return (
    <Card>
      <CardHeader>
        <CardTitle>{t("performance.blocking.title")}</CardTitle>
        <p className="text-muted-foreground text-sm">{t("performance.blocking.subtitle")}</p>
      </CardHeader>
      <CardContent className="space-y-6">
        {isEmpty ? (
          <div className="flex flex-col items-center justify-center gap-3 py-10 text-center">
            <LockIcon className="text-muted-foreground size-8" />
            <div className="space-y-1">
              <p className="font-medium">{t("performance.blocking.empty.title")}</p>
              <p className="text-muted-foreground text-sm">
                {t("performance.blocking.empty.hint")}
              </p>
            </div>
          </div>
        ) : (
          <>
            {sqlChains.length > 0 && (
              <section className="space-y-2">
                <h3 className="text-sm font-medium">{t("performance.blocking.sql.heading")}</h3>
                <SqlChainTable rows={sqlChains} attributionMap={attributionMap} />
              </section>
            )}
            {oneCBlocked.length > 0 && (
              <section className="space-y-2">
                <h3 className="text-sm font-medium">{t("performance.blocking.onec.heading")}</h3>
                <OneCBlockedTable sessions={oneCBlocked} />
              </section>
            )}
          </>
        )}
      </CardContent>
    </Card>
  );
}

// SQL-цепочки: заблокированные запросы с бейджами «ждёт сеанс N» / «блокирует» / «1С» + клиент базы
// и длительность ожидания. Переиспользует ClientCell и i18n-ключи блокировок таблицы запросов.
function SqlChainTable({
  rows,
  attributionMap,
}: {
  rows: SqlLockChainRow[];
  attributionMap: ReturnType<typeof buildAttributionMap>;
}) {
  const { t } = useTranslation();
  return (
    <div className="rounded-md border">
      <Table>
        <TableHeader>
          <TableRow>
            <TableHead>{t("performance.blocking.cols.session")}</TableHead>
            <TableHead>{t("performance.blocking.cols.client")}</TableHead>
            <TableHead className="w-24 text-right">
              {t("performance.blocking.cols.elapsed")}
            </TableHead>
          </TableRow>
        </TableHeader>
        <TableBody>
          {rows.map(({ request, blocks }) => (
            <TableRow key={request.sessionId}>
              <TableCell>
                <div className="flex flex-wrap items-center gap-1">
                  <span className="font-mono text-xs tabular-nums">{request.sessionId}</span>
                  {request.blockingSessionId !== null && (
                    <StatusBadge variant="danger">
                      {t("performance.sql.requests.blockedBy", { id: request.blockingSessionId })}
                    </StatusBadge>
                  )}
                  {blocks && (
                    <StatusBadge variant="warning">
                      {t("performance.sql.requests.blocks")}
                    </StatusBadge>
                  )}
                  {request.isOneC && (
                    <StatusBadge variant="info">{t("performance.sql.oneCBadge")}</StatusBadge>
                  )}
                </div>
              </TableCell>
              <TableCell>
                <ClientCell
                  databaseName={request.databaseName}
                  attribution={attributionFor(request.databaseName, attributionMap)}
                />
              </TableCell>
              <TableCell className="text-muted-foreground text-right tabular-nums">
                {formatMs(request.elapsedMs)}
              </TableCell>
            </TableRow>
          ))}
        </TableBody>
      </Table>
    </div>
  );
}

// 1С-заблокированные сеансы: №/пользователь/хост + бейдж «Заблокирован» с разбором, чем именно
// (СУБД-блокировка и/или управляемая) — показываем те поля, что ≠0/≠null (может быть и оба).
function OneCBlockedTable({ sessions }: { sessions: OneCSessionLoad[] }) {
  const { t } = useTranslation();
  return (
    <div className="rounded-md border">
      <Table>
        <TableHeader>
          <TableRow>
            <TableHead className="w-16">{t("performance.blocking.cols.number")}</TableHead>
            <TableHead>{t("performance.blocking.cols.user")}</TableHead>
            <TableHead>{t("performance.blocking.cols.host")}</TableHead>
            <TableHead>{t("performance.blocking.cols.blocked")}</TableHead>
          </TableRow>
        </TableHeader>
        <TableBody>
          {sessions.map((s) => (
            <TableRow key={s.sessionId}>
              <TableCell className="font-mono text-xs tabular-nums">
                {s.sessionNumber ?? "—"}
              </TableCell>
              <TableCell>{s.userName.trim() || t("performance.onec.noUser")}</TableCell>
              <TableCell className="text-muted-foreground">{s.host || "—"}</TableCell>
              <TableCell>
                <div className="flex flex-wrap items-center gap-1">
                  {(s.blockedByDbms ?? 0) !== 0 && (
                    <StatusBadge variant="danger">
                      {t("performance.blocking.onec.byDbms", { id: s.blockedByDbms })}
                    </StatusBadge>
                  )}
                  {(s.blockedByLs ?? 0) !== 0 && (
                    <StatusBadge variant="danger">
                      {t("performance.blocking.onec.byLs", { id: s.blockedByLs })}
                    </StatusBadge>
                  )}
                </div>
              </TableCell>
            </TableRow>
          ))}
        </TableBody>
      </Table>
    </div>
  );
}
