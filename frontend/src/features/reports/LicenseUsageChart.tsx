import { format } from "date-fns";
import { ru } from "date-fns/locale";
import { useMemo } from "react";
import { useTranslation } from "react-i18next";
import {
  Area,
  CartesianGrid,
  ComposedChart,
  Legend,
  Line,
  ResponsiveContainer,
  Tooltip,
  XAxis,
  YAxis,
} from "recharts";
import { Skeleton } from "@/components/ui/skeleton";
import type { LicenseUsageBucketPoint } from "./types";

// Первое применение recharts в проекте (вводит паттерн графиков). Один компонент на
// оба режима — контракт ряда у сводки и drill-down одинаков. Серии:
//  • consumedMax — заливка (наполнение пула лицензий);
//  • consumedAvg — линия поверх заливки (средняя нагрузка в бакете);
//  • limit       — пунктирная линия-потолок.
// Палитра зеркалит семантику проекта (sky=инфо, emerald=норма, rose=граница/лимит).
const COLOR_MAX = "#0ea5e9"; // sky-500
const COLOR_AVG = "#059669"; // emerald-600
const COLOR_LIMIT = "#f43f5e"; // rose-500

interface ChartRow {
  label: string;
  consumedMax: number;
  consumedAvg: number;
  limit: number;
}

interface LicenseUsageChartProps {
  buckets: LicenseUsageBucketPoint[];
  isLoading: boolean;
}

/** График потребления лицензий во времени. Вызывается только для непустого ряда —
 *  empty-state живёт в родительских секциях (сводка/детализация). */
export function LicenseUsageChart({ buckets, isLoading }: LicenseUsageChartProps) {
  const { t } = useTranslation();

  const data = useMemo<ChartRow[]>(
    () =>
      buckets.map((b) => ({
        label: format(new Date(b.bucketStartUtc), "dd.MM HH:mm", { locale: ru }),
        consumedMax: b.consumedMax,
        // Среднее по бакету — дробное; округляем до десятых для читаемости оси/подсказки.
        consumedAvg: Math.round(b.consumedAvg * 10) / 10,
        limit: b.limit,
      })),
    [buckets]
  );

  if (isLoading) {
    return <Skeleton className="h-[320px] w-full" />;
  }

  return (
    <ResponsiveContainer width="100%" height={320}>
      <ComposedChart data={data} margin={{ top: 8, right: 16, bottom: 8, left: 0 }}>
        <defs>
          <linearGradient id="reportsMaxFill" x1="0" y1="0" x2="0" y2="1">
            <stop offset="0%" stopColor={COLOR_MAX} stopOpacity={0.35} />
            <stop offset="100%" stopColor={COLOR_MAX} stopOpacity={0.03} />
          </linearGradient>
        </defs>
        <CartesianGrid strokeDasharray="3 3" className="stroke-border" />
        <XAxis
          dataKey="label"
          tick={{ fontSize: 12 }}
          minTickGap={24}
          className="text-muted-foreground"
        />
        <YAxis allowDecimals={false} tick={{ fontSize: 12 }} className="text-muted-foreground" />
        <Tooltip contentStyle={{ fontSize: 12 }} labelClassName="text-muted-foreground" />
        <Legend wrapperStyle={{ fontSize: 12 }} />
        <Area
          type="monotone"
          dataKey="consumedMax"
          name={t("reports.chart.consumedMax")}
          stroke={COLOR_MAX}
          fill="url(#reportsMaxFill)"
          strokeWidth={2}
        />
        <Line
          type="monotone"
          dataKey="consumedAvg"
          name={t("reports.chart.consumedAvg")}
          stroke={COLOR_AVG}
          strokeWidth={2}
          dot={false}
        />
        <Line
          type="monotone"
          dataKey="limit"
          name={t("reports.chart.limit")}
          stroke={COLOR_LIMIT}
          strokeWidth={2}
          strokeDasharray="6 4"
          dot={false}
        />
      </ComposedChart>
    </ResponsiveContainer>
  );
}
