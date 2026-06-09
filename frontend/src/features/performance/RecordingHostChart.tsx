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
import { toHostChartRows } from "./recordingAggregation";
import type { RecordingSample } from "./types";

// Палитра зеркалит семантику раздела (sky=ЦП, emerald=память, amber=латентность диска).
const COLOR_CPU = "#0ea5e9"; // sky-500
const COLOR_RAM = "#059669"; // emerald-600
const COLOR_DISK = "#d97706"; // amber-600

interface RecordingHostChartProps {
  samples: RecordingSample[];
}

/**
 * График host-метрик записи во времени (MLC-071, recharts — паттерн `LicenseUsageChart`). ЦП% и
 * «память занято %» на левой оси 0–100 (сопоставимы); латентность диска (мс) на правой оси —
 * разные единицы, поэтому два axisId. Точки сэмплов первичного измерения дают дельта-нули — это
 * нормально для начала записи, отдельно не маскируем (видно по графику).
 */
export function RecordingHostChart({ samples }: RecordingHostChartProps) {
  const { t } = useTranslation();

  const data = useMemo(
    () =>
      toHostChartRows(samples).map((r) => ({
        ...r,
        label: format(new Date(r.sampleUtc), "dd.MM HH:mm:ss", { locale: ru }),
      })),
    [samples]
  );

  return (
    <ResponsiveContainer width="100%" height={300}>
      <ComposedChart data={data} margin={{ top: 8, right: 8, bottom: 8, left: 0 }}>
        <defs>
          <linearGradient id="recordingCpuFill" x1="0" y1="0" x2="0" y2="1">
            <stop offset="0%" stopColor={COLOR_CPU} stopOpacity={0.35} />
            <stop offset="100%" stopColor={COLOR_CPU} stopOpacity={0.03} />
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
          yAxisId="pct"
          domain={[0, 100]}
          tick={{ fontSize: 12 }}
          className="text-muted-foreground"
          unit="%"
        />
        <YAxis
          yAxisId="ms"
          orientation="right"
          tick={{ fontSize: 12 }}
          className="text-muted-foreground"
          unit=" мс"
        />
        <Tooltip contentStyle={{ fontSize: 12 }} labelClassName="text-muted-foreground" />
        <Legend wrapperStyle={{ fontSize: 12 }} />
        <Area
          yAxisId="pct"
          type="monotone"
          dataKey="cpuPercent"
          name={t("performance.recording.chart.cpu")}
          stroke={COLOR_CPU}
          fill="url(#recordingCpuFill)"
          strokeWidth={2}
          dot={false}
        />
        <Line
          yAxisId="pct"
          type="monotone"
          dataKey="memoryUsedPercent"
          name={t("performance.recording.chart.ram")}
          stroke={COLOR_RAM}
          strokeWidth={2}
          dot={false}
        />
        <Line
          yAxisId="ms"
          type="monotone"
          dataKey="diskLatencyMs"
          name={t("performance.recording.chart.disk")}
          stroke={COLOR_DISK}
          strokeWidth={2}
          strokeDasharray="6 4"
          dot={false}
        />
      </ComposedChart>
    </ResponsiveContainer>
  );
}
