import { type ReactNode, useMemo } from "react";
import { useTranslation } from "react-i18next";
import { Link } from "react-router";
import { Line, LineChart, ResponsiveContainer } from "recharts";
import { RelativeTime } from "@/components/ui/RelativeTime";
import { StatusBadge, type StatusBadgeVariant } from "@/components/ui/StatusBadge";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Skeleton } from "@/components/ui/skeleton";
import { Tooltip, TooltipContent, TooltipProvider, TooltipTrigger } from "@/components/ui/tooltip";
import { useLicenseUsage } from "@/features/reports/useLicenseUsage";
import { cn } from "@/lib/utils";
import { AttentionWidget } from "./AttentionWidget";
import { HostHealthCard } from "./HostHealthCard";
import { LicenseTrendCard } from "./LicenseTrendCard";
import { RecentActivityCard } from "./RecentActivityCard";
import { ServerHealthCard } from "@/features/server/ServerHealthCard";
import { lastNDaysRange } from "./trendsRange";
import type { DashboardRasHealth, DashboardSummaryResponse } from "./types";
import { useDashboardSummary } from "./useDashboardSummary";

const SPARKLINE_COLOR = "#0ea5e9"; // sky-500 — тот же «инфо», что у пика лицензий

export function DashboardPage() {
  const { t } = useTranslation();
  const { data, isLoading, isError, isFetching, refetch } = useDashboardSummary();

  // MLC-186c — фиксированный 7-дневный диапазон тренда «Обзора». Считаем ОДИН раз
  // (useMemo с пустыми зависимостями), иначе новый объект каждый рендер даст новый
  // react-query-key и бесконечный рефетч. Один license-запрос на страницу —
  // его данные делят трендовая карточка и KPI-спарклайн.
  const trendsRange = useMemo(() => lastNDaysRange(7), []);
  const licenseUsage = useLicenseUsage(trendsRange);

  const licenseBuckets = licenseUsage.data?.buckets;

  // Мини-спарклайн под значением KPI «Использовано лицензий»: пик потребления за
  // 7 дней без осей/сетки/тултипа. Нет данных → не рендерим (sparkline=undefined).
  const licenseSparkline =
    licenseBuckets && licenseBuckets.length > 0 ? (
      <ResponsiveContainer width="100%" height={36}>
        <LineChart data={licenseBuckets} margin={{ top: 4, right: 0, bottom: 0, left: 0 }}>
          <Line
            type="monotone"
            dataKey="consumedMax"
            stroke={SPARKLINE_COLOR}
            strokeWidth={1.5}
            dot={false}
            isAnimationActive={false}
          />
        </LineChart>
      </ResponsiveContainer>
    ) : undefined;

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

        {/* MLC-198 — Фаза 2 редизайна: «Требует внимания» поднят под шапку (actionable
            сигналы важнее тихих KPI). Единый виджет (квоты, дрейф кластера, диск бэкапов,
            RAS, факт лицензий); summary передаём пропсом (DashboardPage уже грузит его) —
            виджет не дублирует запрос summary (MLC-186b). */}
        <AttentionWidget summary={data} />

        <KpiGrid
          data={data}
          isLoading={isLoading}
          isFetching={isFetching}
          licenseSparkline={licenseSparkline}
        />

        {/* MLC-198 — один тренд «Обзора»: использование лицензий за 7 дней (на всю
            ширину). Тренд размера баз убран — его дом теперь «Базы → Размер баз»
            (MLC-196b). License-запрос делится с KPI-спарклайном (общий диапазон). */}
        <LicenseTrendCard
          buckets={licenseUsage.data?.buckets}
          peakConsumed={licenseUsage.data?.peakConsumed}
          peakLimit={licenseUsage.data?.peakLimit}
          isLoading={licenseUsage.isLoading}
        />

        {/* Строка состояния системы (MLC-085, аудит §3.4): RAS-статус + здоровье хоста +
            состояние сервера (MLC-214) — три равные карты на всю ширину. */}
        <div className="grid grid-cols-1 gap-4 lg:grid-cols-3">
          <RasHealthCard data={data} isLoading={isLoading} isFetching={isFetching} />
          <HostHealthCard isFetching={isFetching} />
          <ServerHealthCard isFetching={isFetching} />
        </div>

        {/* MLC-198 — лента свежей активности на всю ширину (соседний блок «топ-клиенты»
            убран: дублировал «Сеансы → По клиентам», MLC-196a). */}
        <RecentActivityCard />
      </div>
    </TooltipProvider>
  );
}

interface KpiGridProps {
  data: DashboardSummaryResponse | undefined;
  isLoading: boolean;
  isFetching: boolean;
  // MLC-186c — мини-спарклайн под значением KPI «Использовано лицензий» (undefined,
  // пока ряд не накоплен).
  licenseSparkline?: ReactNode;
}

// KPI-карточки кликабельны (MLC-085): каждая ведёт в раздел, отвечающий за её
// число. «Свободно лицензий» — производная от «Использовано», обе ведут в вид
// «Использование за период» дома «Сеансы» (MLC-196c: «Отчёты» растворены, ADR-53).
function KpiGrid({ data, isLoading, isFetching, licenseSparkline }: KpiGridProps) {
  const { t } = useTranslation();

  return (
    <div className="grid grid-cols-1 gap-4 sm:grid-cols-2 lg:grid-cols-5">
      <KpiCard
        label={t("dashboard.kpi.tenants")}
        value={data ? data.tenantsActive : undefined}
        secondary={data ? t("dashboard.kpi.tenantsLabel", { total: data.tenantsTotal }) : undefined}
        to="/tenants"
        isLoading={isLoading}
        isFetching={isFetching}
      />
      <KpiCard
        label={t("dashboard.kpi.infobases")}
        value={data?.infobasesTotal}
        to="/infobases"
        isLoading={isLoading}
        isFetching={isFetching}
      />
      <KpiCard
        label={t("dashboard.kpi.sessions")}
        value={data?.sessionsActiveTotal}
        to="/sessions"
        isLoading={isLoading}
        isFetching={isFetching}
        live
      />
      <KpiCard
        label={t("dashboard.kpi.consumed")}
        value={data?.licensesConsumedTotal}
        to="/sessions?view=usage"
        isLoading={isLoading}
        isFetching={isFetching}
        sparkline={licenseSparkline}
      />
      <KpiCard
        label={t("dashboard.kpi.available")}
        value={data?.licensesAvailableTotal}
        to="/sessions?view=usage"
        isLoading={isLoading}
        isFetching={isFetching}
      />
    </div>
  );
}

interface KpiCardProps {
  label: string;
  value: number | undefined;
  secondary?: string;
  to: string;
  isLoading: boolean;
  isFetching: boolean;
  // MLC-186c — мини-спарклайн под значением (общий license-ряд «Обзора»).
  sparkline?: ReactNode;
  // MLC-186c — «живой» индикатор: число опрашивается онлайн (активные сеансы — poll 5с).
  live?: boolean;
}

function KpiCard({
  label,
  value,
  secondary,
  to,
  isLoading,
  isFetching,
  sparkline,
  live,
}: KpiCardProps) {
  return (
    <Link
      to={to}
      className="focus-visible:ring-ring block rounded-xl focus-visible:ring-2 focus-visible:outline-none"
    >
      <Card
        className={cn(
          "hover:bg-muted/50 h-full gap-2 py-4 transition-colors",
          isFetching && !isLoading && "opacity-90"
        )}
      >
        <CardHeader className="px-4 pb-0">
          <CardTitle className="text-muted-foreground flex items-center gap-1.5 text-xs font-medium tracking-wide uppercase">
            {label}
            {live && (
              <span
                aria-hidden="true"
                data-testid="kpi-live-dot"
                className="inline-block size-2 shrink-0 animate-pulse rounded-full bg-emerald-500"
              />
            )}
          </CardTitle>
        </CardHeader>
        <CardContent className="px-4">
          {isLoading || value === undefined ? (
            <Skeleton className="h-8 w-16" />
          ) : (
            <div className="text-3xl font-semibold tabular-nums">{value}</div>
          )}
          {secondary && <p className="text-muted-foreground mt-1 text-xs">{secondary}</p>}
          {sparkline && (
            <div className="mt-2 h-9" data-testid="kpi-sparkline">
              {sparkline}
            </div>
          )}
        </CardContent>
      </Card>
    </Link>
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
// 3. healthy === false → «Сбой» danger + видимая actionable-подсказка (UX-17,
//    MLC-121): что случилось и переход в «Параметры»; lastErrorMessage остаётся
//    во вторичном тултипе.
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
            {/* UX-17 — видимая actionable-подсказка при недоступности кластера (а не
                только тултип с lastErrorMessage): что случилось и куда идти чинить. */}
            {!ras.healthy && (
              <div className="border-status-danger/30 bg-status-danger/5 mt-1 space-y-1 rounded-md border p-2">
                <p className="text-status-danger text-xs">{t("dashboard.ras.hint")}</p>
                <Link to="/settings" className="text-primary inline-block text-xs underline">
                  {t("dashboard.ras.settingsLink")}
                </Link>
              </div>
            )}
          </div>
        )}
      </CardContent>
    </Card>
  );
}
