import { useMemo } from "react";
import { useTranslation } from "react-i18next";
import { Link } from "react-router";
import {
  Bar,
  CartesianGrid,
  Cell,
  ComposedChart,
  ReferenceLine,
  ResponsiveContainer,
  Tooltip,
  XAxis,
  YAxis,
} from "recharts";
import type { TenantConsumptionRow } from "./types";
import { quotaSeverity } from "@/lib/quota";

// Палитра зеркалит semantic-статусы проекта (quota.ts / StatusBadge):
//   ok      → emerald-600 (норма)
//   warning → amber-600   (близко к лимиту, ≥75%)
//   danger  → rose-500    (превышение/критично, ≥90%)
// Лимит-линия — пунктир rose-500 (аналогично LicenseUsageChart).
const COLOR_OK = "#059669"; // emerald-600
const COLOR_WARN = "#d97706"; // amber-600
const COLOR_DANGER = "#f43f5e"; // rose-500
const COLOR_LIMIT = "#f43f5e"; // rose-500

function severityColor(consumed: number, limit: number): string {
  const s = quotaSeverity(consumed, limit);
  if (s === "danger") return COLOR_DANGER;
  if (s === "warning") return COLOR_WARN;
  return COLOR_OK;
}

interface ChartRow {
  name: string;
  tenantId: string;
  consumed: number;
  limit: number;
}

interface TopTenantsChartProps {
  data: TenantConsumptionRow[];
}

/**
 * График потребления лицензий топ-клиентов (MLC-143).
 *
 * Паттерн: повторяет LicenseUsageChart / RecordingHostChart — ResponsiveContainer,
 * CartesianGrid со stroke-border, Tooltip, одна Bar с Cell-цветами по severity квоты.
 * Лимит показан ReferenceLine (пунктир) только если лимиты одинаковы для всех
 * клиентов; при разных лимитах линии не рисуются (разные базы сравнения).
 *
 * Каждый лейбл на оси X — кликабельная ссылка; реализована через кастомный XAxisTick.
 */
export function TopTenantsChart({ data }: TopTenantsChartProps) {
  const { t } = useTranslation();

  const rows = useMemo<ChartRow[]>(
    () =>
      data.map((d) => ({
        name: d.tenantName,
        tenantId: d.tenantId,
        consumed: d.consumed,
        limit: d.limit,
      })),
    [data]
  );

  // Единый лимит для ReferenceLine — только если одинаков у всех строк.
  const sharedLimit = useMemo(() => {
    if (rows.length === 0) return null;
    const first = rows[0].limit;
    return rows.every((r) => r.limit === first) ? first : null;
  }, [rows]);

  const maxConsumed = useMemo(() => Math.max(...rows.map((r) => r.consumed), 1), [rows]);
  const yDomain = [0, sharedLimit != null ? Math.max(sharedLimit, maxConsumed) : maxConsumed] as [
    number,
    number,
  ];

  return (
    <ResponsiveContainer width="100%" height={rows.length * 40 + 60}>
      <ComposedChart
        data={rows}
        layout="vertical"
        margin={{ top: 4, right: 40, bottom: 4, left: 0 }}
      >
        <CartesianGrid strokeDasharray="3 3" horizontal={false} className="stroke-border" />
        <XAxis
          type="number"
          domain={yDomain}
          allowDecimals={false}
          tick={{ fontSize: 12 }}
          className="text-muted-foreground"
          label={{
            value: t("dashboard.topTenants.chart.axisLicenses"),
            position: "insideBottomRight",
            offset: -4,
            fontSize: 11,
          }}
        />
        <YAxis
          type="category"
          dataKey="name"
          width={0}
          tick={false}
          axisLine={false}
          tickLine={false}
        />
        <Tooltip
          contentStyle={{ fontSize: 12 }}
          labelClassName="text-muted-foreground font-medium"
          formatter={(value, _name, props) => {
            const row = props.payload as ChartRow | undefined;
            const num = typeof value === "number" ? value : Number(value ?? 0);
            if (!row) return [num, t("dashboard.topTenants.chart.consumed")];
            return [
              t("dashboard.topTenants.consumedOf", {
                consumed: row.consumed,
                limit: row.limit,
              }) + ` (${row.limit > 0 ? Math.round((row.consumed / row.limit) * 100) : 0}%)`,
              t("dashboard.topTenants.chart.consumed"),
            ];
          }}
        />
        {sharedLimit != null && sharedLimit > 0 && (
          <ReferenceLine
            x={sharedLimit}
            stroke={COLOR_LIMIT}
            strokeWidth={1.5}
            strokeDasharray="6 4"
            label={{
              value: t("dashboard.topTenants.chart.limit", { limit: sharedLimit }),
              position: "insideTopRight",
              fontSize: 10,
              fill: COLOR_LIMIT,
            }}
          />
        )}
        <Bar
          dataKey="consumed"
          name={t("dashboard.topTenants.chart.consumed")}
          radius={[0, 3, 3, 0]}
        >
          {rows.map((row) => (
            <Cell key={row.tenantId} fill={severityColor(row.consumed, row.limit)} />
          ))}
        </Bar>
      </ComposedChart>
    </ResponsiveContainer>
  );
}

/** Кликабельная метка клиента слева от бара — рендерится отдельным списком над/под графиком
 *  (recharts не поддерживает ссылки внутри svg-лейблов). */
export function TopTenantsLegend({ data }: { data: TenantConsumptionRow[] }) {
  return (
    <ul className="space-y-[1px]" style={{ marginBottom: 2 }}>
      {data.map((row) => (
        <li key={row.tenantId} style={{ height: 40 }} className="flex items-center">
          <Link
            to={`/tenants/${row.tenantId}`}
            className="hover:text-primary max-w-[180px] truncate text-sm font-medium hover:underline"
            title={row.tenantName}
          >
            {row.tenantName}
          </Link>
        </li>
      ))}
    </ul>
  );
}
