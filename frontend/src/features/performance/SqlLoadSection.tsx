import { useMemo } from "react";
import { DatabaseIcon } from "lucide-react";
import { useTranslation } from "react-i18next";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Skeleton } from "@/components/ui/skeleton";
import { SqlActiveRequestsTable } from "./SqlActiveRequestsTable";
import { SqlDatabaseIoTable, SqlWaitsTable } from "./SqlContentionTables";
import { SqlDatabaseLoadTable } from "./SqlDatabaseLoadTable";
import { SqlStatusBanner } from "./SqlStatusBanner";
import { buildAttributionMap } from "./sqlLoad";
import { useSqlPerformance } from "./useSqlPerformance";

/**
 * Live-секция «1С грузит SQL?» (MLC-069, ADR-26, Фаза 3) — drill-down раздела «Быстродействие»,
 * когда узкое место — MSSQL. Активные запросы (топ по ЦП, блокировки, признак 1С) + нагрузка по
 * базам/клиентам + конкуренция за ресурсы (дельта ожиданий и IO-stall). Отдельный live-источник
 * от host- и 1С-снимков; собственная Zod-граница в `useSqlPerformance`. Pull-по-требованию
 * (polling 5с, prev-снимок между poll'ами).
 *
 * Degraded по статусу пробы (нет VIEW SERVER STATE / SQL недоступен) → честный баннер вместо
 * тихого «всё спокойно». `measuring` (первый poll) → дельты ожиданий/IO ещё нет, показываем
 * «измеряю…», но активные запросы (мгновенны) рисуем сразу.
 */
export function SqlLoadSection({ paused = false }: { paused?: boolean }) {
  const { t } = useTranslation();
  const { data, isLoading, isError } = useSqlPerformance(paused);

  const attributionMap = useMemo(
    () => buildAttributionMap(data?.databases ?? []),
    [data?.databases]
  );

  const snapshot = data?.snapshot;
  const isOk = snapshot?.status === "Ok";
  const isIdle =
    isOk &&
    !snapshot.measuring &&
    snapshot.activeRequests.length === 0 &&
    snapshot.databaseIo.length === 0 &&
    snapshot.topWaits.length === 0;

  return (
    <Card>
      <CardHeader>
        <CardTitle>{t("performance.sql.title")}</CardTitle>
        <p className="text-muted-foreground text-sm">{t("performance.sql.subtitle")}</p>
      </CardHeader>
      <CardContent className="space-y-6">
        {isError && !data && (
          <p className="text-muted-foreground text-sm">{t("performance.sql.errors.loadFailed")}</p>
        )}

        {isLoading && !data ? (
          <Skeleton className="h-40 w-full" />
        ) : snapshot ? (
          snapshot.status !== "Ok" ? (
            <SqlStatusBanner status={snapshot.status} />
          ) : isIdle ? (
            <div className="flex flex-col items-center justify-center gap-3 py-10 text-center">
              <DatabaseIcon className="text-muted-foreground size-8" />
              <div className="space-y-1">
                <p className="font-medium">{t("performance.sql.idle.title")}</p>
                <p className="text-muted-foreground text-sm">{t("performance.sql.idle.hint")}</p>
              </div>
            </div>
          ) : (
            <>
              <section className="space-y-2">
                <h3 className="text-sm font-medium">{t("performance.sql.requests.heading")}</h3>
                <SqlActiveRequestsTable
                  requests={snapshot.activeRequests}
                  attributionMap={attributionMap}
                />
              </section>

              <section className="space-y-2">
                <h3 className="text-sm font-medium">{t("performance.sql.byDatabase.heading")}</h3>
                <SqlDatabaseLoadTable
                  requests={snapshot.activeRequests}
                  attributionMap={attributionMap}
                />
              </section>

              <section className="space-y-2">
                <h3 className="text-sm font-medium">{t("performance.sql.contention.heading")}</h3>
                {snapshot.measuring ? (
                  <p className="text-muted-foreground text-sm">{t("performance.sql.measuring")}</p>
                ) : (
                  <div className="grid gap-4 lg:grid-cols-2">
                    <SqlWaitsTable waits={snapshot.topWaits} />
                    <SqlDatabaseIoTable io={snapshot.databaseIo} />
                  </div>
                )}
              </section>
            </>
          )
        ) : null}
      </CardContent>
    </Card>
  );
}
