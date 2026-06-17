import { useMemo } from "react";
import { useTranslation } from "react-i18next";
import {
  Area,
  CartesianGrid,
  ComposedChart,
  ResponsiveContainer,
  Tooltip,
  type TooltipContentProps,
  XAxis,
  YAxis,
} from "recharts";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Skeleton } from "@/components/ui/skeleton";
import { formatBytes } from "@/lib/formatBytes";
import { formatBucketAxisLabel } from "@/features/reports/reportsUrlState";
import type { DatabaseSizePoint } from "@/features/reports/types";

// Компактная версия DatabaseSizeChart (MLC-186c): высота ~180, ось Y в ГБ,
// tooltip с точным размером через formatBytes. Палитра: sky=размер (как в отчёте).
const COLOR_TOTAL = "#0ea5e9"; // sky-500
const GB = 1024 ** 3;

interface ChartRow {
  label: string;
  totalBytes: number;
}

interface DatabaseSizeTrendCardProps {
  points: DatabaseSizePoint[] | undefined;
  isLoading: boolean;
}

function SizeTooltip({ active, payload, label }: TooltipContentProps) {
  const { t } = useTranslation();
  if (!active || !payload || payload.length === 0) return null;
  const raw = payload[0]?.value;
  const value = typeof raw === "number" ? raw : Number(raw ?? 0);
  return (
    <div className="bg-background rounded-md border p-2 text-xs shadow-sm">
      <p className="text-muted-foreground mb-1">{label}</p>
      <p className="font-medium tabular-nums">
        {t("reports.size.chart.total")}: {formatBytes(value)}
      </p>
    </div>
  );
}

/** Дельта размера за период: разница totalBytes между первой и последней точкой ряда.
 *  <2 точек → дельты нет («—»). Знак «+/−» для роста/уменьшения. */
function weekDeltaLabel(points: DatabaseSizePoint[]): string | null {
  if (points.length < 2) return null;
  const delta = points[points.length - 1].totalBytes - points[0].totalBytes;
  const sign = delta >= 0 ? "+" : "−";
  return `${sign}${formatBytes(Math.abs(delta))}`;
}

/** Трендовая карточка «Рост размера баз» на «Обзоре» (MLC-186c). Заголовок — дельта
 *  за период; тело — компактный график итога размера во времени. */
export function DatabaseSizeTrendCard({ points, isLoading }: DatabaseSizeTrendCardProps) {
  const { t } = useTranslation();

  const data = useMemo<ChartRow[]>(
    () =>
      (points ?? []).map((p) => ({
        label: formatBucketAxisLabel(p.atUtc),
        totalBytes: p.totalBytes,
      })),
    [points]
  );

  const hasData = data.length > 0;
  const delta = useMemo(() => weekDeltaLabel(points ?? []), [points]);

  return (
    <Card>
      <CardHeader>
        <CardTitle>{t("dashboard.trends.databaseSizeGrowth.title")}</CardTitle>
        {!isLoading && hasData && (
          <p className="text-muted-foreground text-sm tabular-nums">
            {t("dashboard.trends.databaseSizeGrowth.weekDelta", { delta: delta ?? "—" })}
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
                <linearGradient id="dashDbSizeTotalFill" x1="0" y1="0" x2="0" y2="1">
                  <stop offset="0%" stopColor={COLOR_TOTAL} stopOpacity={0.35} />
                  <stop offset="100%" stopColor={COLOR_TOTAL} stopOpacity={0.03} />
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
                tick={{ fontSize: 11 }}
                width={48}
                className="text-muted-foreground"
                tickFormatter={(value: number) => `${(value / GB).toFixed(1)} ГБ`}
              />
              <Tooltip content={SizeTooltip} />
              <Area
                type="monotone"
                dataKey="totalBytes"
                stroke={COLOR_TOTAL}
                fill="url(#dashDbSizeTotalFill)"
                strokeWidth={2}
                isAnimationActive={false}
              />
            </ComposedChart>
          </ResponsiveContainer>
        )}
      </CardContent>
    </Card>
  );
}
