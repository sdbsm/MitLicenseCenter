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
import { Skeleton } from "@/components/ui/skeleton";
import { formatBytes } from "@/lib/formatBytes";
import { formatBucketAxisLabel } from "./reportsUrlState";
import type { DatabaseSizePoint } from "./types";

// MLC-185f: рост размера во времени. Основная серия — totalBytes (итог data+log);
// ось Y подписываем в ГБ (значения хранилища исчисляются гигабайтами), tooltip даёт
// точный размер через единый formatBytes (КБ/МБ/ГБ, MLC-185d). Палитра: sky = размер
// (тот же «инфо», что у пика лицензий) — раздел один, визуальный язык общий.
const COLOR_TOTAL = "#0ea5e9"; // sky-500
const GB = 1024 ** 3;

interface ChartRow {
  label: string;
  totalBytes: number;
}

interface DatabaseSizeChartProps {
  points: DatabaseSizePoint[];
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

/** График роста размера баз во времени (итог по хосту или по клиенту). Вызывается
 *  только для непустого ряда — empty-state живёт в родительских секциях. Ось Y — в ГБ. */
export function DatabaseSizeChart({ points, isLoading }: DatabaseSizeChartProps) {
  const data = useMemo<ChartRow[]>(
    () =>
      points.map((p) => ({
        label: formatBucketAxisLabel(p.atUtc),
        totalBytes: p.totalBytes,
      })),
    [points]
  );

  if (isLoading) {
    return <Skeleton className="h-[320px] w-full" />;
  }

  return (
    <ResponsiveContainer width="100%" height={320}>
      <ComposedChart data={data} margin={{ top: 8, right: 16, bottom: 8, left: 8 }}>
        <defs>
          <linearGradient id="dbSizeTotalFill" x1="0" y1="0" x2="0" y2="1">
            <stop offset="0%" stopColor={COLOR_TOTAL} stopOpacity={0.35} />
            <stop offset="100%" stopColor={COLOR_TOTAL} stopOpacity={0.03} />
          </linearGradient>
        </defs>
        <CartesianGrid strokeDasharray="3 3" className="stroke-border" />
        <XAxis
          dataKey="label"
          tick={{ fontSize: 12 }}
          minTickGap={24}
          className="text-muted-foreground"
        />
        <YAxis
          tick={{ fontSize: 12 }}
          className="text-muted-foreground"
          width={56}
          tickFormatter={(value: number) => `${(value / GB).toFixed(1)} ГБ`}
        />
        <Tooltip content={SizeTooltip} />
        <Area
          type="monotone"
          dataKey="totalBytes"
          stroke={COLOR_TOTAL}
          fill="url(#dbSizeTotalFill)"
          strokeWidth={2}
        />
      </ComposedChart>
    </ResponsiveContainer>
  );
}
