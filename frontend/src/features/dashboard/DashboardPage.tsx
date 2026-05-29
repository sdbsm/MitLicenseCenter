import { useTranslation } from "react-i18next";
import { RelativeTime } from "@/components/ui/RelativeTime";
import { StatusBadge, type StatusBadgeVariant } from "@/components/ui/StatusBadge";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Progress } from "@/components/ui/progress";
import { Skeleton } from "@/components/ui/skeleton";
import { Tooltip, TooltipContent, TooltipProvider, TooltipTrigger } from "@/components/ui/tooltip";
import { cn } from "@/lib/utils";
import type { DashboardRasHealth, DashboardSummaryResponse, TenantConsumptionRow } from "./types";
import { useDashboardSummary } from "./useDashboardSummary";

function progressColorClass(percent: number): string {
  if (percent >= 90) return "[&>[data-slot=progress-indicator]]:bg-rose-500";
  if (percent >= 75) return "[&>[data-slot=progress-indicator]]:bg-amber-500";
  return "";
}

export function DashboardPage() {
  const { t } = useTranslation();
  const { data, isLoading, isError, isFetching, refetch } = useDashboardSummary();

  return (
    <TooltipProvider delayDuration={150}>
      <div className="space-y-6">
        <div className="flex items-start justify-between gap-4">
          <div className="space-y-1">
            <h2 className="text-2xl font-semibold tracking-tight">{t("dashboard.title")}</h2>
            <p className="text-muted-foreground text-sm">{t("dashboard.subtitle")}</p>
          </div>
          {data && (
            <span className="text-muted-foreground text-sm">
              {t("dashboard.refresh.updated")}{" "}
              <RelativeTime value={new Date()} thresholdAmberSec={60} />
            </span>
          )}
        </div>

        {isError && (
          <div className="border-destructive/40 bg-destructive/5 rounded-md border p-4 text-sm">
            <p className="font-medium">{t("dashboard.refresh.error")}</p>
            <button
              type="button"
              className="text-primary mt-1 underline"
              onClick={() => {
                void refetch();
              }}
            >
              {t("common.refresh")}
            </button>
          </div>
        )}

        <KpiGrid data={data} isLoading={isLoading} isFetching={isFetching} />
        <TopTenantsCard data={data?.topTenantsByConsumption ?? null} isLoading={isLoading} />
      </div>
    </TooltipProvider>
  );
}

interface KpiGridProps {
  data: DashboardSummaryResponse | undefined;
  isLoading: boolean;
  isFetching: boolean;
}

function KpiGrid({ data, isLoading, isFetching }: KpiGridProps) {
  const { t } = useTranslation();

  return (
    <div className="grid grid-cols-1 gap-4 sm:grid-cols-2 lg:grid-cols-3 xl:grid-cols-6">
      <KpiCard
        label={t("dashboard.kpi.tenants")}
        value={data ? data.tenantsActive : undefined}
        secondary={data ? t("dashboard.kpi.tenantsLabel", { total: data.tenantsTotal }) : undefined}
        isLoading={isLoading}
        isFetching={isFetching}
      />
      <KpiCard
        label={t("dashboard.kpi.infobases")}
        value={data?.infobasesTotal}
        isLoading={isLoading}
        isFetching={isFetching}
      />
      <KpiCard
        label={t("dashboard.kpi.sessions")}
        value={data?.sessionsActiveTotal}
        isLoading={isLoading}
        isFetching={isFetching}
      />
      <KpiCard
        label={t("dashboard.kpi.consumed")}
        value={data?.licensesConsumedTotal}
        isLoading={isLoading}
        isFetching={isFetching}
      />
      <KpiCard
        label={t("dashboard.kpi.available")}
        value={data?.licensesAvailableTotal}
        isLoading={isLoading}
        isFetching={isFetching}
      />
      <RasHealthCard data={data} isLoading={isLoading} isFetching={isFetching} />
    </div>
  );
}

interface KpiCardProps {
  label: string;
  value: number | undefined;
  secondary?: string;
  isLoading: boolean;
  isFetching: boolean;
}

function KpiCard({ label, value, secondary, isLoading, isFetching }: KpiCardProps) {
  return (
    <Card className={cn("gap-2 py-4", isFetching && !isLoading && "opacity-90")}>
      <CardHeader className="px-4 pb-0">
        <CardTitle className="text-muted-foreground text-xs font-medium tracking-wide uppercase">
          {label}
        </CardTitle>
      </CardHeader>
      <CardContent className="px-4">
        {isLoading || value === undefined ? (
          <Skeleton className="h-8 w-16" />
        ) : (
          <div className="text-3xl font-semibold tabular-nums">{value}</div>
        )}
        {secondary && <p className="text-muted-foreground mt-1 text-xs">{secondary}</p>}
      </CardContent>
    </Card>
  );
}

interface RasHealthCardProps {
  data: DashboardSummaryResponse | undefined;
  isLoading: boolean;
  isFetching: boolean;
}

// Stage 5 PR 5.1 (ADR-16): заменяет ClusterCard. Три визуальных состояния:
// 1. ras === undefined ИЛИ lastCheckedAtUtc === null → «Проверка…» neutral
// 2. healthy === true → «OK» success
// 3. healthy === false → «Сбой» danger + tooltip с lastErrorMessage
function RasHealthCard({ data, isLoading, isFetching }: RasHealthCardProps) {
  const { t } = useTranslation();
  const ras: DashboardRasHealth | undefined = data?.ras;

  const stillChecking = !ras || ras.lastCheckedAtUtc === null;
  const variant: StatusBadgeVariant = stillChecking
    ? "neutral"
    : ras.healthy
      ? "success"
      : "danger";

  const label = stillChecking
    ? t("dashboard.ras.checking")
    : ras.healthy
      ? t("dashboard.ras.ok")
      : t("dashboard.ras.failed");

  const showError = ras && !ras.healthy && ras.lastErrorMessage;

  return (
    <Card className={cn("gap-2 py-4", isFetching && !isLoading && "opacity-90")}>
      <CardHeader className="px-4 pb-0">
        <CardTitle className="text-muted-foreground text-xs font-medium tracking-wide uppercase">
          {t("dashboard.kpi.rasStatus")}
        </CardTitle>
      </CardHeader>
      <CardContent className="px-4">
        {isLoading || !ras ? (
          <Skeleton className="h-8 w-24" />
        ) : (
          <div className="space-y-1.5">
            <div className="flex items-center gap-2">
              <StatusBadge variant={variant}>{label}</StatusBadge>
              {showError && (
                <Tooltip>
                  <TooltipTrigger asChild>
                    <span className="text-muted-foreground cursor-help text-xs underline">
                      {t("dashboard.ras.lastError")}
                    </span>
                  </TooltipTrigger>
                  <TooltipContent className="max-w-xs">
                    <span className="text-xs">{ras.lastErrorMessage}</span>
                  </TooltipContent>
                </Tooltip>
              )}
            </div>
            {ras.lastCheckedAtUtc && (
              <p className="text-muted-foreground text-xs">
                {t("dashboard.ras.lastChecked")}: <RelativeTime value={ras.lastCheckedAtUtc} />
              </p>
            )}
            {!ras.healthy && ras.consecutiveFailures > 1 && (
              <p className="text-muted-foreground text-xs">
                {t("dashboard.ras.consecutiveFailures", { count: ras.consecutiveFailures })}
              </p>
            )}
          </div>
        )}
      </CardContent>
    </Card>
  );
}

interface TopTenantsCardProps {
  data: TenantConsumptionRow[] | null;
  isLoading: boolean;
}

function TopTenantsCard({ data, isLoading }: TopTenantsCardProps) {
  const { t } = useTranslation();

  return (
    <Card>
      <CardHeader>
        <CardTitle>{t("dashboard.topTenants.title")}</CardTitle>
      </CardHeader>
      <CardContent>
        {isLoading ? (
          <div className="space-y-3">
            {Array.from({ length: 5 }).map((_, idx) => (
              <Skeleton key={idx} className="h-6 w-full" />
            ))}
          </div>
        ) : !data || data.length === 0 ? (
          <p className="text-muted-foreground text-sm">{t("dashboard.topTenants.empty")}</p>
        ) : (
          <ul className="space-y-3">
            {data.map((row) => (
              <li key={row.tenantId} className="grid grid-cols-[1fr_auto] items-center gap-3">
                <div className="min-w-0 space-y-1.5">
                  <p className="truncate text-sm font-medium">{row.tenantName}</p>
                  <Progress value={row.percent} className={progressColorClass(row.percent)} />
                </div>
                <p className="text-muted-foreground text-sm tabular-nums">
                  {t("dashboard.topTenants.consumedOf", {
                    consumed: row.consumed,
                    limit: row.limit,
                  })}{" "}
                  ({row.percent}%)
                </p>
              </li>
            ))}
          </ul>
        )}
      </CardContent>
    </Card>
  );
}
