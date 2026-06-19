import { useTranslation } from "react-i18next";
import { Link } from "react-router";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Skeleton } from "@/components/ui/skeleton";
import { cn } from "@/lib/utils";
import { ServerHealthBadge } from "./ServerHealthBadge";
import { useMaintenanceBackups } from "./useMaintenanceBackups";
import { alertingOutcomes, useMaintenancePlans } from "./useMaintenancePlans";
import { useServerStatus } from "./useServerStatus";

/**
 * Компактная плашка здоровья узла на «Обзоре» (MLC-214): светофор по overall +
 * сколько серверов 1С запущено. Вся карточка — ссылка на /server. Делит queryKey
 * со страницей «Сервер» (один запрос на оба места). Скелетон/ошибка как HostHealthCard.
 *
 * MLC-217: дополнительно поднимает сигнал «обслуживание» — «бэкап устарел» (свежесть из
 * MLC-216) и/или «план обслуживания упал» (планы из MLC-217). Сигнал композируется на FE
 * (контракт GET /server/status не трогаем — parity 213): отдельная строка-предупреждение
 * под светофором, вся карточка ведёт на /server. Деградация проб (нет прав / SQL Agent
 * недоступен) сигнал НЕ поднимает — алертим только подтверждённые провалы/устаревание.
 */
export function ServerHealthCard({ isFetching }: { isFetching?: boolean }) {
  const { t } = useTranslation();
  const { data, isLoading, isError } = useServerStatus();
  const { data: backups } = useMaintenanceBackups();
  const { data: plans } = useMaintenancePlans();

  const total = data?.oneCServers.length ?? 0;
  const runningCount = data?.oneCServers.filter((s) => s.running).length ?? 0;

  // Сигнал обслуживания: устаревший бэкап (только при status Ok — degraded не алертим) и/или
  // упавший/просроченный под-план (Failed/Overdue, совпадает с SubplanRunPolicy.IsAlerting).
  const backupStale = backups?.status === "Ok" && backups.databases.some((db) => db.isStale);
  const planFailed =
    plans?.status === "Ok" &&
    plans.plans.some((p) => p.subplans.some((sp) => alertingOutcomes.has(sp.outcome)));
  const maintenanceAlert = backupStale || planFailed;

  return (
    <Link
      to="/server"
      className="focus-visible:ring-ring block rounded-xl focus-visible:ring-2 focus-visible:outline-none"
    >
      <Card
        className={cn(
          "hover:bg-muted/50 h-full gap-2 py-4 transition-colors",
          isFetching && !isLoading && "opacity-90"
        )}
      >
        <CardHeader className="px-4 pb-0">
          <CardTitle className="text-muted-foreground text-xs font-medium tracking-wide uppercase">
            {t("server.dashboard.title")}
          </CardTitle>
        </CardHeader>
        <CardContent className="px-4">
          {isError ? (
            <p className="text-muted-foreground text-sm">{t("server.dashboard.error")}</p>
          ) : isLoading || !data ? (
            <Skeleton className="h-8 w-24" />
          ) : (
            <div className="space-y-1.5">
              <ServerHealthBadge overall={data.overall} />
              <p className="text-muted-foreground text-xs">
                {t("server.dashboard.onecRunning", { running: runningCount, total })}
              </p>
              {maintenanceAlert && (
                <p className="text-status-danger text-xs font-medium">
                  {planFailed && backupStale
                    ? t("server.dashboard.maintenance.both")
                    : planFailed
                      ? t("server.dashboard.maintenance.planFailed")
                      : t("server.dashboard.maintenance.backupStale")}
                </p>
              )}
            </div>
          )}
        </CardContent>
      </Card>
    </Link>
  );
}
