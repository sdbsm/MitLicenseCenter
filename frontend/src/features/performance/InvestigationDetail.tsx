import { useTranslation } from "react-i18next";
import { ArrowLeftIcon, FileTextIcon } from "lucide-react";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Skeleton } from "@/components/ui/skeleton";
import { StatusBadge } from "@/components/ui/StatusBadge";
import { useInfobases } from "@/features/infobases/useInfobases";
import {
  useInvestigationDetail,
  useInvestigationReport,
} from "@/features/investigations/useInvestigations";
import {
  INVESTIGATION_STATUS_VARIANT,
  REPORT_SEVERITY_VARIANT,
  fmtDate,
  fmtSeconds,
} from "@/features/investigations/investigationUtils";
import type {
  DbmsLockAnalysisResult,
  ExceptionAnalysisResult,
  Finding,
  FindingKind,
  LockAnalysisResult,
  SlowQueryAnalysisResult,
} from "@/features/investigations/types";
import {
  lockAnalysisResultSchema,
  slowQueryAnalysisResultSchema,
  exceptionAnalysisResultSchema,
  dbmsLockAnalysisResultSchema,
} from "@/features/investigations/types";

/**
 * Карточка «Дело» — экран 3 (MLC-243, ADR-57, спека §Экран 3).
 *
 * Показывается при выборе дела из списка. Загружает detail + report по id.
 * Кнопка «Отчёт» — открывает экран «Отчёт» (MLC-244) через onOpenReport(id).
 * Кнопка «Назад» — возвращает к списку через onBack().
 */

interface InvestigationDetailProps {
  investigationId: string;
  onBack: () => void;
  /** Открыть экран «Отчёт» (MLC-244). */
  onOpenReport: (id: string) => void;
}

// ─── Helpers ──────────────────────────────────────────────────────────────────

/** Безопасно парсит result по kind; возвращает null при несовместимости. */
function parseResult(finding: Finding): unknown {
  try {
    switch (finding.kind) {
      case "ManagedLocks":
        return lockAnalysisResultSchema.parse(finding.result);
      case "SlowQueries":
        return slowQueryAnalysisResultSchema.parse(finding.result);
      case "Exceptions":
        return exceptionAnalysisResultSchema.parse(finding.result);
      case "DbmsLocks":
        return dbmsLockAnalysisResultSchema.parse(finding.result);
    }
  } catch {
    return null;
  }
}

/** Получает типизированный result по kind; null если не распарсился. */
function getFinding<T>(findings: Finding[], kind: FindingKind): T | null {
  const f = findings.find((x) => x.kind === kind);
  if (!f) return null;
  const result = f.result ?? parseResult(f);
  return result as T | null;
}

// ─── Блок блокировок 1С ───────────────────────────────────────────────────────

function Locks1cBlock({ result }: { result: LockAnalysisResult }) {
  const { t } = useTranslation();
  const hasEdges = result.waitEdges.length > 0;
  const hasTimeouts = result.timeouts.length > 0;
  const hasDeadlocks = result.deadlocks.length > 0;

  if (!hasEdges && !hasTimeouts && !hasDeadlocks) {
    return (
      <p className="text-muted-foreground text-sm">{t("investigations.detail.locks1c.empty")}</p>
    );
  }

  return (
    <div className="space-y-4">
      {/* Рёбра ожидания */}
      {hasEdges && (
        <div className="space-y-2">
          <p className="text-sm font-medium">{t("investigations.detail.locks1c.waitEdgesTitle")}</p>
          <div className="space-y-2">
            {result.waitEdges.map((edge, i) => (
              <div key={i} className="bg-muted/40 rounded-md p-3 text-sm">
                <div className="grid grid-cols-[auto_1fr] gap-x-3 gap-y-1">
                  {edge.waitingSessionId && (
                    <>
                      <span className="text-muted-foreground">
                        {t("investigations.detail.locks1c.waiting")}
                      </span>
                      <span className="font-mono">{edge.waitingSessionId}</span>
                    </>
                  )}
                  {edge.blockingConnections && (
                    <>
                      <span className="text-muted-foreground">
                        {t("investigations.detail.locks1c.blocking")}
                      </span>
                      <span className="font-mono">{edge.blockingConnections}</span>
                    </>
                  )}
                  {edge.regions && (
                    <>
                      <span className="text-muted-foreground">
                        {t("investigations.detail.locks1c.resource")}
                      </span>
                      <span>{edge.regions}</span>
                    </>
                  )}
                  {edge.waitDurationSeconds != null && (
                    <>
                      <span className="text-muted-foreground">
                        {t("investigations.detail.locks1c.duration")}
                      </span>
                      <span>{fmtSeconds(edge.waitDurationSeconds)}</span>
                    </>
                  )}
                  {edge.waitingUser && (
                    <>
                      <span className="text-muted-foreground">
                        {t("investigations.detail.locks1c.user")}
                      </span>
                      <span>{edge.waitingUser}</span>
                    </>
                  )}
                  {edge.context && (
                    <>
                      <span className="text-muted-foreground">
                        {t("investigations.detail.locks1c.context")}
                      </span>
                      <span className="truncate font-mono text-xs">{edge.context}</span>
                    </>
                  )}
                </div>
              </div>
            ))}
          </div>
        </div>
      )}

      {/* Таймауты */}
      {hasTimeouts && (
        <div className="space-y-2">
          <p className="text-sm font-medium">{t("investigations.detail.locks1c.timeoutsTitle")}</p>
          <div className="space-y-2">
            {result.timeouts.map((to, i) => (
              <div key={i} className="border-l-2 border-amber-400 pl-3 text-sm">
                <span className="text-muted-foreground">
                  {to.user ?? t("investigations.detail.locks1c.noData")}
                </span>
                {to.regions && <span className="ml-2">· {to.regions}</span>}
                {to.waitDurationSeconds != null && (
                  <span className="ml-2">· {fmtSeconds(to.waitDurationSeconds)}</span>
                )}
              </div>
            ))}
          </div>
        </div>
      )}

      {/* Дедлоки */}
      {hasDeadlocks && (
        <div className="space-y-2">
          <p className="text-sm font-medium">{t("investigations.detail.locks1c.deadlocksTitle")}</p>
          <div className="space-y-2">
            {result.deadlocks.map((dl, i) => (
              <div key={i} className="border-l-2 border-rose-400 pl-3 text-sm">
                <span className="text-muted-foreground">
                  {dl.user ?? t("investigations.detail.locks1c.noData")}
                </span>
                {dl.regions && <span className="ml-2">· {dl.regions}</span>}
                {dl.durationSeconds != null && (
                  <span className="ml-2">· {fmtSeconds(dl.durationSeconds)}</span>
                )}
              </div>
            ))}
          </div>
        </div>
      )}
    </div>
  );
}

// ─── Блок долгих запросов ─────────────────────────────────────────────────────

function SlowQueriesBlock({ result }: { result: SlowQueryAnalysisResult }) {
  const { t } = useTranslation();

  if (result.topQueries.length === 0) {
    return (
      <p className="text-muted-foreground text-sm">
        {t("investigations.detail.slowQueries.empty")}
      </p>
    );
  }

  return (
    <div className="space-y-3">
      {result.topQueries.map((q, i) => (
        <details key={i} className="group rounded-md border">
          <summary className="flex cursor-pointer items-start gap-3 p-3 text-sm marker:content-none">
            <span className="text-muted-foreground w-16 shrink-0 tabular-nums">
              {fmtSeconds(q.durationSeconds)}
            </span>
            <span className="text-muted-foreground min-w-0 flex-1 truncate">
              {q.context ?? t("investigations.detail.slowQueries.sql")}
            </span>
          </summary>
          <div className="space-y-2 border-t p-3 text-sm">
            {q.context && (
              <div>
                <p className="text-muted-foreground mb-1 text-xs">
                  {t("investigations.detail.slowQueries.context")}
                </p>
                <pre className="bg-muted overflow-x-auto rounded p-2 text-xs break-words whitespace-pre-wrap">
                  {q.context}
                </pre>
              </div>
            )}
            {q.sql ? (
              <div>
                <p className="text-muted-foreground mb-1 text-xs">
                  {t("investigations.detail.slowQueries.sql")}
                </p>
                <pre className="bg-muted overflow-x-auto rounded p-2 text-xs break-words whitespace-pre-wrap">
                  {q.sql}
                </pre>
              </div>
            ) : (
              <p className="text-muted-foreground text-xs">
                {t("investigations.detail.slowQueries.noSql")}
              </p>
            )}
            {q.planText ? (
              <div>
                <p className="text-muted-foreground mb-1 text-xs">
                  {t("investigations.detail.slowQueries.planTitle")}
                </p>
                <pre className="bg-muted max-h-48 overflow-x-auto rounded p-2 text-xs break-words whitespace-pre-wrap">
                  {q.planText}
                </pre>
              </div>
            ) : (
              <p className="text-muted-foreground text-xs">
                {t("investigations.detail.slowQueries.noPlan")}
              </p>
            )}
          </div>
        </details>
      ))}
    </div>
  );
}

// ─── Блок исключений ──────────────────────────────────────────────────────────

function ExceptionsBlock({ result }: { result: ExceptionAnalysisResult }) {
  const { t } = useTranslation();

  if (result.topExceptions.length === 0) {
    return (
      <p className="text-muted-foreground text-sm">{t("investigations.detail.exceptions.empty")}</p>
    );
  }

  return (
    <div className="space-y-2">
      {result.topExceptions.map((g, i) => (
        <div key={i} className="bg-muted/40 rounded-md p-3 text-sm">
          <div className="flex items-start justify-between gap-2">
            <div className="min-w-0 flex-1 space-y-1">
              {g.exceptionType && (
                <p className="font-mono text-xs font-medium">{g.exceptionType}</p>
              )}
              <p className="text-muted-foreground text-xs">{g.sampleDescr ?? g.normalizedDescr}</p>
              {g.sampleContext && (
                <pre className="bg-muted mt-1 overflow-x-auto rounded p-1.5 text-xs break-words whitespace-pre-wrap">
                  {g.sampleContext}
                </pre>
              )}
            </div>
            <div className="flex shrink-0 flex-col items-end gap-1">
              <span className="text-sm font-semibold tabular-nums">{g.count}</span>
              {g.isDatabaseException && (
                <span
                  className="text-muted-foreground text-xs"
                  title={t("investigations.detail.exceptions.dbExceptionHint")}
                >
                  {t("investigations.detail.exceptions.dbException")}
                </span>
              )}
            </div>
          </div>
        </div>
      ))}
    </div>
  );
}

// ─── Блок СУБД-блокировок ─────────────────────────────────────────────────────

function DbmsLocksBlock({ result }: { result: DbmsLockAnalysisResult }) {
  const { t } = useTranslation();

  if (result.waitEdges.length === 0) {
    return (
      <p className="text-muted-foreground text-sm">{t("investigations.detail.dbmsLocks.empty")}</p>
    );
  }

  return (
    <div className="space-y-3">
      {result.waitEdges.map((edge, i) => (
        <div key={i} className="bg-muted/40 space-y-2 rounded-md p-3 text-sm">
          <div className="flex items-center gap-2">
            {!edge.sourceMatched && (
              <Badge variant="outline" className="text-xs text-amber-700 dark:text-amber-400">
                {t("investigations.detail.dbmsLocks.unmatched")}
              </Badge>
            )}
          </div>
          <div className="grid grid-cols-1 gap-2 sm:grid-cols-2">
            {/* Жертва */}
            <div className="space-y-1">
              <p className="text-muted-foreground text-xs font-medium">
                {t("investigations.detail.dbmsLocks.victim")}
                {edge.victimConnectId && (
                  <span className="ml-1 font-mono">#{edge.victimConnectId}</span>
                )}
              </p>
              {edge.victimSql && (
                <pre className="bg-muted max-h-32 overflow-x-auto rounded p-1.5 text-xs break-words whitespace-pre-wrap">
                  {edge.victimSql}
                </pre>
              )}
              {edge.victimContext && (
                <p className="text-muted-foreground truncate text-xs">{edge.victimContext}</p>
              )}
            </div>
            {/* Источник */}
            {edge.sourceMatched && (
              <div className="space-y-1">
                <p className="text-muted-foreground text-xs font-medium">
                  {t("investigations.detail.dbmsLocks.source")}
                  {edge.sourceConnectId && (
                    <span className="ml-1 font-mono">#{edge.sourceConnectId}</span>
                  )}
                </p>
                {edge.sourceSql && (
                  <pre className="bg-muted max-h-32 overflow-x-auto rounded p-1.5 text-xs break-words whitespace-pre-wrap">
                    {edge.sourceSql}
                  </pre>
                )}
                {edge.sourceContext && (
                  <p className="text-muted-foreground truncate text-xs">{edge.sourceContext}</p>
                )}
              </div>
            )}
          </div>
        </div>
      ))}
    </div>
  );
}

// ─── Главный компонент ────────────────────────────────────────────────────────

export function InvestigationDetail({
  investigationId,
  onBack,
  onOpenReport,
}: InvestigationDetailProps) {
  const { t } = useTranslation();

  const { data: detail, isLoading: detailLoading } = useInvestigationDetail(investigationId);
  const { data: report, isLoading: reportLoading } = useInvestigationReport(investigationId);

  // Список инфобаз для разрешения infobaseId → имя
  const { data: infobasesData } = useInfobases(null, null, false, 1, 100);

  if (detailLoading && !detail) {
    return <Skeleton className="h-64 w-full" />;
  }

  if (!detail) {
    return (
      <div className="text-muted-foreground py-12 text-center text-sm">{t("common.noData")}</div>
    );
  }

  const { summary, findings } = detail;

  // Разрешаем scope
  const scopeLabel = summary.infobaseId
    ? (infobasesData?.items.find((ib) => ib.id === summary.infobaseId)?.name ?? summary.infobaseId)
    : t("investigations.list.scopeAll");

  // Типизированные result по kind
  const locksResult = getFinding<LockAnalysisResult>(findings, "ManagedLocks");
  const slowQueriesResult = getFinding<SlowQueryAnalysisResult>(findings, "SlowQueries");
  const exceptionsResult = getFinding<ExceptionAnalysisResult>(findings, "Exceptions");
  const dbmsLocksResult = getFinding<DbmsLockAnalysisResult>(findings, "DbmsLocks");

  return (
    <div className="space-y-6">
      {/* Шапка */}
      <div className="flex flex-wrap items-start justify-between gap-3">
        <div className="space-y-2">
          <Button variant="ghost" size="sm" onClick={onBack} className="-ml-2">
            <ArrowLeftIcon className="size-4" />
            {t("investigations.detail.back")}
          </Button>
          <div className="flex flex-wrap items-center gap-2">
            <span className="text-muted-foreground font-mono text-xs">
              {summary.id.slice(0, 8)}
            </span>
            <StatusBadge variant={INVESTIGATION_STATUS_VARIANT[summary.status]}>
              {t(`investigations.status.${summary.status}`)}
            </StatusBadge>
            <Badge variant="outline">{t(`investigations.scenario.${summary.scenario}`)}</Badge>
          </div>
          <dl className="text-muted-foreground grid grid-cols-[auto_1fr] gap-x-3 gap-y-0.5 text-sm">
            <dt>{t("investigations.detail.periodLabel")}</dt>
            <dd>
              {fmtDate(summary.startedAtUtc)}
              {summary.stoppedAtUtc && ` – ${fmtDate(summary.stoppedAtUtc)}`}
            </dd>
            <dt>{t("investigations.detail.targetLabel")}</dt>
            <dd>{scopeLabel}</dd>
            <dt>{t("investigations.detail.startedByLabel")}</dt>
            <dd>{summary.startedBy}</dd>
          </dl>
        </div>
        <Button variant="outline" size="sm" onClick={() => onOpenReport(investigationId)}>
          <FileTextIcon className="size-4" />
          {t("investigations.detail.reportBtn")}
        </Button>
      </div>

      {/* Вердикт и рекомендации */}
      <Card>
        <CardHeader>
          <CardTitle className="text-base">{t("investigations.detail.verdict.title")}</CardTitle>
        </CardHeader>
        <CardContent>
          {reportLoading && !report ? (
            <p className="text-muted-foreground text-sm">
              {t("investigations.detail.verdict.loading")}
            </p>
          ) : report && report.items.length > 0 ? (
            <div className="space-y-4">
              {report.items.map((item, i) => (
                <div key={i} className="space-y-1">
                  <div className="flex items-center gap-2">
                    <StatusBadge variant={REPORT_SEVERITY_VARIANT[item.severity]}>
                      {t(`investigations.severity.${item.severity}`)}
                    </StatusBadge>
                    <span className="text-sm font-medium">{item.headline}</span>
                  </div>
                  <p className="text-muted-foreground text-sm">{item.recommendation}</p>
                </div>
              ))}
            </div>
          ) : (
            <p className="text-muted-foreground text-sm">
              {t("investigations.detail.verdict.empty")}
            </p>
          )}
        </CardContent>
      </Card>

      {/* Сводка-метрики */}
      <Card>
        <CardHeader>
          <CardTitle className="text-base">{t("investigations.detail.metrics.title")}</CardTitle>
        </CardHeader>
        <CardContent>
          <dl className="grid grid-cols-2 gap-x-6 gap-y-1 text-sm sm:grid-cols-3">
            {locksResult && (
              <>
                <div className="contents">
                  <dt className="text-muted-foreground">
                    {t("investigations.detail.metrics.locks")}
                  </dt>
                  <dd className="font-medium tabular-nums">{locksResult.waitEdges.length}</dd>
                </div>
                <div className="contents">
                  <dt className="text-muted-foreground">
                    {t("investigations.detail.metrics.lockTimeouts")}
                  </dt>
                  <dd className="font-medium tabular-nums">{locksResult.timeouts.length}</dd>
                </div>
                <div className="contents">
                  <dt className="text-muted-foreground">
                    {t("investigations.detail.metrics.lockDeadlocks")}
                  </dt>
                  <dd className="font-medium tabular-nums">{locksResult.deadlocks.length}</dd>
                </div>
              </>
            )}
            {slowQueriesResult && (
              <>
                <div className="contents">
                  <dt className="text-muted-foreground">
                    {t("investigations.detail.metrics.slowQueries")}
                  </dt>
                  <dd className="font-medium tabular-nums">
                    {slowQueriesResult.topQueries.length}
                  </dd>
                </div>
                <div className="contents">
                  <dt className="text-muted-foreground">
                    {t("investigations.detail.metrics.queryGroups")}
                  </dt>
                  <dd className="font-medium tabular-nums">
                    {slowQueriesResult.similarGroups.length}
                  </dd>
                </div>
              </>
            )}
            {exceptionsResult && (
              <>
                <div className="contents">
                  <dt className="text-muted-foreground">
                    {t("investigations.detail.metrics.exceptions")}
                  </dt>
                  <dd className="font-medium tabular-nums">{exceptionsResult.totalExcpEvents}</dd>
                </div>
                <div className="contents">
                  <dt className="text-muted-foreground">
                    {t("investigations.detail.metrics.dbExceptions")}
                  </dt>
                  <dd className="font-medium tabular-nums">
                    {exceptionsResult.databaseExceptionEvents}
                  </dd>
                </div>
              </>
            )}
            {dbmsLocksResult && (
              <div className="contents">
                <dt className="text-muted-foreground">
                  {t("investigations.detail.metrics.dbmsLockEdges")}
                </dt>
                <dd className="font-medium tabular-nums">{dbmsLocksResult.waitEdges.length}</dd>
              </div>
            )}
          </dl>
        </CardContent>
      </Card>

      {/* Блок блокировок 1С */}
      {locksResult && (
        <Card>
          <CardHeader>
            <CardTitle className="text-base">{t("investigations.detail.locks1c.title")}</CardTitle>
          </CardHeader>
          <CardContent>
            <Locks1cBlock result={locksResult} />
          </CardContent>
        </Card>
      )}

      {/* Блок долгих запросов */}
      {slowQueriesResult && (
        <Card>
          <CardHeader>
            <CardTitle className="text-base">
              {t("investigations.detail.slowQueries.title")}
            </CardTitle>
          </CardHeader>
          <CardContent>
            <SlowQueriesBlock result={slowQueriesResult} />
          </CardContent>
        </Card>
      )}

      {/* Блок исключений */}
      {exceptionsResult && (
        <Card>
          <CardHeader>
            <CardTitle className="text-base">
              {t("investigations.detail.exceptions.title")}
            </CardTitle>
          </CardHeader>
          <CardContent>
            <ExceptionsBlock result={exceptionsResult} />
          </CardContent>
        </Card>
      )}

      {/* Блок СУБД-блокировок — отдельный, не смешивать с 1С */}
      {dbmsLocksResult && (
        <Card>
          <CardHeader>
            <CardTitle className="text-base">
              {t("investigations.detail.dbmsLocks.title")}
            </CardTitle>
          </CardHeader>
          <CardContent>
            <DbmsLocksBlock result={dbmsLocksResult} />
          </CardContent>
        </Card>
      )}
    </div>
  );
}
