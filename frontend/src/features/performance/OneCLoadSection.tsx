import { GaugeIcon } from "lucide-react";
import { useTranslation } from "react-i18next";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Skeleton } from "@/components/ui/skeleton";
import { OneCProcessesTable } from "./OneCProcessesTable";
import { OneCSessionsTable } from "./OneCSessionsTable";
import { useOneCLoad } from "./useOneCLoad";

/**
 * Live-секция «кто грузит внутри 1С» (MLC-067, ADR-26) — drill-down раздела «Быстродействие»,
 * когда узкое место — сама 1С. Топ сеансов по нагрузке + рабочие процессы. Pull-по-требованию
 * (polling 5с, prev-снимок между poll'ами). Отдельный live-источник от host-снимка; собственная
 * Zod-граница в `useOneCLoad`. Пустые списки = «нет активной нагрузки» (бэкенд без Available-флага,
 * решение MLC-066: отсутствие сигнала ≠ ошибка, обычно rac не настроен/нет активных сеансов).
 */
export function OneCLoadSection({ paused = false }: { paused?: boolean }) {
  const { t } = useTranslation();
  const { data, isLoading, isError } = useOneCLoad(paused);

  const isEmpty = data && data.sessions.length === 0 && data.processes.length === 0;

  return (
    <Card>
      <CardHeader>
        <CardTitle>{t("performance.onec.title")}</CardTitle>
        <p className="text-muted-foreground text-sm">{t("performance.onec.subtitle")}</p>
      </CardHeader>
      <CardContent className="space-y-6">
        {isError && !data && (
          <p className="text-muted-foreground text-sm">{t("performance.onec.errors.loadFailed")}</p>
        )}

        {isLoading && !data ? (
          <Skeleton className="h-40 w-full" />
        ) : data ? (
          isEmpty ? (
            <div className="flex flex-col items-center justify-center gap-3 py-10 text-center">
              <GaugeIcon className="text-muted-foreground size-8" />
              <div className="space-y-1">
                <p className="font-medium">{t("performance.onec.idle.title")}</p>
                <p className="text-muted-foreground text-sm">{t("performance.onec.idle.hint")}</p>
              </div>
            </div>
          ) : (
            <>
              <section className="space-y-2">
                <h3 className="text-sm font-medium">{t("performance.onec.sessions.heading")}</h3>
                <OneCSessionsTable sessions={data.sessions} capturedAtUtc={data.capturedAtUtc} />
              </section>
              <section className="space-y-2">
                <h3 className="text-sm font-medium">{t("performance.onec.processes.heading")}</h3>
                <OneCProcessesTable processes={data.processes} />
              </section>
            </>
          )
        ) : null}
      </CardContent>
    </Card>
  );
}
