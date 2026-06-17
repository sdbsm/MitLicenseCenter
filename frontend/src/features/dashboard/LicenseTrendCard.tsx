import { useMemo } from "react";
import { useTranslation } from "react-i18next";
import {
  Area,
  CartesianGrid,
  ComposedChart,
  Line,
  ResponsiveContainer,
  Tooltip,
  XAxis,
  YAxis,
} from "recharts";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Skeleton } from "@/components/ui/skeleton";
import { formatBucketAxisLabel } from "@/features/reports/reportsUrlState";
import type { LicenseUsageBucketPoint } from "@/features/reports/types";

// Компактная версия LicenseUsageChart (MLC-186c) для трендовой карточки «Обзора»:
// высота ~180, без легенды, минимальные оси. Палитра зеркалит отчёт: sky=пик,
// rose=лимит (пунктир).
const COLOR_MAX = "#0ea5e9"; // sky-500
const COLOR_LIMIT = "#f43f5e"; // rose-500

interface ChartRow {
  label: string;
  consumedMax: number;
  limit: number;
}

interface LicenseTrendCardProps {
  buckets: LicenseUsageBucketPoint[] | undefined;
  peakConsumed: number | undefined;
  peakLimit: number | undefined;
  isLoading: boolean;
}

/** Трендовая карточка «Использование лицензий (7 дней)» на «Обзоре» (MLC-186c).
 *  Переиспользует тот же license-запрос, что и спарклайн KPI (диапазон фиксирован
 *  один раз в DashboardPage). */
export function LicenseTrendCard({
  buckets,
  peakConsumed,
  peakLimit,
  isLoading,
}: LicenseTrendCardProps) {
  const { t } = useTranslation();

  const data = useMemo<ChartRow[]>(
    () =>
      (buckets ?? []).map((b) => ({
        label: formatBucketAxisLabel(b.bucketStartUtc),
        consumedMax: b.consumedMax,
        limit: b.limit,
      })),
    [buckets]
  );

  const hasData = data.length > 0;

  return (
    <Card>
      <CardHeader>
        <CardTitle>{t("dashboard.trends.licenseConsumption7d.title")}</CardTitle>
        {!isLoading && hasData && peakConsumed !== undefined && peakLimit !== undefined && (
          <p className="text-muted-foreground text-sm tabular-nums">
            {t("dashboard.trends.licenseConsumption7d.peak", {
              consumed: peakConsumed,
              limit: peakLimit,
            })}
          </p>
        )}
      </CardHeader>
      <CardContent>
        {isLoading ? (
          <Skeleton className="h-[180px] w-full" />
        ) : !hasData ? (
          <p className="text-muted-foreground text-sm">{t("dashboard.trends.empty")}</p>
        ) : (
          <ResponsiveContainer width="100%" height={180}>
            <ComposedChart data={data} margin={{ top: 8, right: 8, bottom: 0, left: 0 }}>
              <defs>
                <linearGradient id="dashLicenseMaxFill" x1="0" y1="0" x2="0" y2="1">
                  <stop offset="0%" stopColor={COLOR_MAX} stopOpacity={0.35} />
                  <stop offset="100%" stopColor={COLOR_MAX} stopOpacity={0.03} />
                </linearGradient>
              </defs>
              <CartesianGrid strokeDasharray="3 3" className="stroke-border" />
              <XAxis
                dataKey="label"
                tick={{ fontSize: 11 }}
                minTickGap={32}
                className="text-muted-foreground"
              />
              <YAxis
                allowDecimals={false}
                width={32}
                tick={{ fontSize: 11 }}
                className="text-muted-foreground"
              />
              <Tooltip contentStyle={{ fontSize: 12 }} labelClassName="text-muted-foreground" />
              <Area
                type="monotone"
                dataKey="consumedMax"
                name={t("reports.chart.consumedMax")}
                stroke={COLOR_MAX}
                fill="url(#dashLicenseMaxFill)"
                strokeWidth={2}
                isAnimationActive={false}
              />
              <Line
                type="monotone"
                dataKey="limit"
                name={t("reports.chart.limit")}
                stroke={COLOR_LIMIT}
                strokeWidth={2}
                strokeDasharray="6 4"
                dot={false}
                isAnimationActive={false}
              />
            </ComposedChart>
          </ResponsiveContainer>
        )}
      </CardContent>
    </Card>
  );
}
