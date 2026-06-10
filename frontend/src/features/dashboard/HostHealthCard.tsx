import { useTranslation } from "react-i18next";
import { Link } from "react-router";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Skeleton } from "@/components/ui/skeleton";
import { cn } from "@/lib/utils";
import { MetricGauge } from "@/features/performance/MetricGauge";
import {
  cpuSaturation,
  diskFillPercent,
  diskMaxLatencySec,
  diskSaturation,
  ramSaturation,
  ramUsedPercent,
} from "@/features/performance/thresholds";
import { useDashboardHostHealth } from "./useDashboardHostHealth";

/**
 * Компактная строка здоровья хоста на дашборде (MLC-085, аудит §3.4): упрощённые
 * гейджи CPU/RAM/диск без детальных подписей — светофор «есть ли проблема».
 * Вся карточка — ссылка на /performance («какая и из-за кого»). Пороги и
 * measuring-семантика — те же, что у SaturationGauges: на первой пробе
 * (Measuring=true) CPU и диск показывают «измеряю…», не нули; RAM мгновенна.
 */
export function HostHealthCard({ isFetching }: { isFetching?: boolean }) {
  const { t } = useTranslation();
  const { data: snapshot, isLoading, isError } = useDashboardHostHealth();

  return (
    <Link
      to="/performance"
      className="focus-visible:ring-ring block rounded-xl focus-visible:ring-2 focus-visible:outline-none lg:col-span-3"
    >
      <Card
        className={cn(
          "hover:bg-muted/50 h-full gap-2 py-4 transition-colors",
          isFetching && !isLoading && "opacity-90"
        )}
      >
        <CardHeader className="px-4 pb-0">
          <CardTitle className="text-muted-foreground text-xs font-medium tracking-wide uppercase">
            {t("dashboard.host.title")}
          </CardTitle>
        </CardHeader>
        <CardContent className="px-4">
          {isError ? (
            <p className="text-muted-foreground text-sm">{t("dashboard.host.error")}</p>
          ) : isLoading || !snapshot ? (
            <div className="grid grid-cols-1 gap-x-8 gap-y-3 sm:grid-cols-3">
              {Array.from({ length: 3 }).map((_, idx) => (
                <Skeleton key={idx} className="h-12 w-full" />
              ))}
            </div>
          ) : (
            <div className="grid grid-cols-1 gap-x-8 gap-y-3 sm:grid-cols-3">
              <MetricGauge
                label={t("performance.saturation.cpu")}
                valueText={t("performance.units.percent", {
                  value: Math.round(snapshot.cpu.totalPercent),
                })}
                fillPercent={snapshot.cpu.totalPercent}
                saturation={cpuSaturation(snapshot.cpu)}
                measuring={snapshot.measuring}
              />
              <MetricGauge
                label={t("performance.saturation.ram")}
                valueText={t("performance.units.percent", {
                  value: Math.round(ramUsedPercent(snapshot.memory)),
                })}
                fillPercent={ramUsedPercent(snapshot.memory)}
                saturation={ramSaturation(snapshot.memory)}
                // RAM-занятость мгновенна — честно рисуется и на первой пробе.
                measuring={false}
              />
              <MetricGauge
                label={t("performance.saturation.disk")}
                valueText={t("performance.units.ms", {
                  value: Math.round(diskMaxLatencySec(snapshot.disk) * 1000),
                })}
                fillPercent={diskFillPercent(snapshot.disk)}
                saturation={diskSaturation(snapshot.disk)}
                measuring={snapshot.measuring}
              />
            </div>
          )}
        </CardContent>
      </Card>
    </Link>
  );
}
