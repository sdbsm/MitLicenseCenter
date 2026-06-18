import { DatabaseIcon } from "lucide-react";
import { useTranslation } from "react-i18next";
import {
  Bar,
  CartesianGrid,
  ComposedChart,
  Legend,
  Line,
  ResponsiveContainer,
  Tooltip,
  XAxis,
  YAxis,
} from "recharts";
import { StatusBadge } from "@/components/ui/StatusBadge";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Progress } from "@/components/ui/progress";
import { Skeleton } from "@/components/ui/skeleton";
import { quotaDisplay } from "@/lib/quota";

// Цвета серий чарт-кита берём из семантической палитры статусов (index.css), а не
// из hex-зоопарка: recharts не наследует CSS-переменные, поэтому ссылаемся на них
// явно через var(--status-*) — монохром нейтральной шкалы не нарушается, цвет несёт
// только статусную семантику (info=ЦП, success=память, warning=диск).
const SERIES_CPU = "var(--status-info)";
const SERIES_MEMORY = "var(--status-success)";
const SERIES_DISK = "var(--status-warning)";

// Статическая фикстура для эталонного графика (не живые данные).
const CHART_DATA = [
  { label: "10:00", cpu: 32, memory: 58, disk: 6 },
  { label: "10:05", cpu: 41, memory: 60, disk: 8 },
  { label: "10:10", cpu: 55, memory: 63, disk: 12 },
  { label: "10:15", cpu: 48, memory: 65, disk: 9 },
  { label: "10:20", cpu: 67, memory: 70, disk: 15 },
  { label: "10:25", cpu: 59, memory: 68, disk: 11 },
];

// Плотный «ридаут хоста» — несколько строк метрик подряд, компактные паддинги.
const READOUT_ROWS = [
  { key: "cpu", value: "59 %" },
  { key: "memory", value: "68 %" },
  { key: "disk", value: "11 мс" },
  { key: "uptime", value: "14 д 06:21" },
] as const;

/**
 * Эталонный «kitchen-sink» экран дизайн-системы (MLC-195, Фаза 0). Единый эталон
 * визуального языка: типошкала, статусы через StatusBadge, две плотности карточек,
 * чарт-кит, состояния (загрузка/пусто/ошибка) и полоса квоты. Не в навигации —
 * открывается по URL `/design`, виден обеим ролям. Данные — статические фикстуры.
 */
export function DesignSystemPage() {
  const { t } = useTranslation();
  const quota = quotaDisplay(280, 320);

  return (
    <div className="space-y-6">
      <div className="space-y-1">
        <h2 className="text-[22px] leading-tight font-medium tracking-tight">
          {t("design.title")}
        </h2>
        <p className="text-muted-foreground text-sm">{t("design.subtitle")}</p>
      </div>

      {/* Типошкала — зафиксированная визуально, каждая строка подписана своей ролью. */}
      <Card>
        <CardHeader>
          <CardTitle className="text-base font-medium">{t("design.typography.title")}</CardTitle>
        </CardHeader>
        <CardContent className="space-y-3">
          <p className="text-muted-foreground text-xs">{t("design.typography.hint")}</p>
          <p className="text-[22px] leading-tight font-medium">{t("design.typography.page")}</p>
          <p className="text-base font-medium">{t("design.typography.section")}</p>
          <p className="text-sm font-normal">{t("design.typography.body")}</p>
          <p className="text-muted-foreground text-xs font-normal">
            {t("design.typography.caption")}
          </p>
          <p className="font-mono text-sm tabular-nums">
            {t("design.typography.numeric")} — 1 234 567.89
          </p>
        </CardContent>
      </Card>

      {/* Статусы — все 5 вариантов StatusBadge с доменными подписями. */}
      <Card>
        <CardHeader>
          <CardTitle className="text-base font-medium">{t("design.statuses.title")}</CardTitle>
        </CardHeader>
        <CardContent className="space-y-3">
          <p className="text-muted-foreground text-xs">{t("design.statuses.hint")}</p>
          <div className="flex flex-wrap gap-2">
            <StatusBadge variant="success">{t("design.statuses.success")}</StatusBadge>
            <StatusBadge variant="warning">{t("design.statuses.warning")}</StatusBadge>
            <StatusBadge variant="danger">{t("design.statuses.danger")}</StatusBadge>
            <StatusBadge variant="info">{t("design.statuses.info")}</StatusBadge>
            <StatusBadge variant="neutral">{t("design.statuses.neutral")}</StatusBadge>
          </div>
        </CardContent>
      </Card>

      {/* Две плотности: тихая KPI-карточка и плотный ридаут хоста. */}
      <section className="space-y-3">
        <h3 className="text-base font-medium">{t("design.density.title")}</h3>
        <p className="text-muted-foreground text-xs">{t("design.density.hint")}</p>
        <div className="grid grid-cols-1 gap-4 sm:grid-cols-2">
          <Card className="gap-2 py-4">
            <CardHeader className="px-4 pb-0">
              <CardTitle className="text-muted-foreground text-xs font-medium tracking-wide uppercase">
                {t("design.density.kpi.label")}
              </CardTitle>
            </CardHeader>
            <CardContent className="px-4">
              <div className="text-3xl font-semibold tabular-nums">280</div>
              <p className="text-muted-foreground mt-1 text-xs">
                {t("design.density.kpi.secondary")}
              </p>
            </CardContent>
          </Card>

          <Card className="gap-2 py-4">
            <CardHeader className="px-4 pb-0">
              <CardTitle className="text-sm font-medium">
                {t("design.density.readout.title")}
              </CardTitle>
            </CardHeader>
            <CardContent className="px-4">
              <dl className="divide-border divide-y text-sm">
                {READOUT_ROWS.map((row) => (
                  <div key={row.key} className="flex items-center justify-between py-1.5">
                    <dt className="text-muted-foreground">
                      {t(`design.density.readout.${row.key}`)}
                    </dt>
                    <dd className="font-mono tabular-nums">{row.value}</dd>
                  </div>
                ))}
              </dl>
            </CardContent>
          </Card>
        </div>
      </section>

      {/* Чарт-кит — recharts с темизацией из docs/06_UI_GUIDE.md §6. */}
      <Card>
        <CardHeader>
          <CardTitle className="text-base font-medium">{t("design.chart.title")}</CardTitle>
        </CardHeader>
        <CardContent className="space-y-3">
          <p className="text-muted-foreground text-xs">{t("design.chart.hint")}</p>
          <ResponsiveContainer width="100%" height={280}>
            <ComposedChart data={CHART_DATA} margin={{ top: 8, right: 8, bottom: 8, left: 0 }}>
              <CartesianGrid strokeDasharray="3 3" className="stroke-border" />
              <XAxis
                dataKey="label"
                tick={{ fontSize: 12 }}
                className="text-muted-foreground"
                minTickGap={16}
              />
              <YAxis tick={{ fontSize: 12 }} className="text-muted-foreground" />
              <Tooltip contentStyle={{ fontSize: 12 }} labelClassName="text-muted-foreground" />
              <Legend wrapperStyle={{ fontSize: 12 }} />
              <Bar
                dataKey="disk"
                name={t("design.chart.disk")}
                fill={SERIES_DISK}
                radius={[2, 2, 0, 0]}
                barSize={14}
              />
              <Line
                type="monotone"
                dataKey="cpu"
                name={t("design.chart.cpu")}
                stroke={SERIES_CPU}
                strokeWidth={2}
                dot={false}
              />
              <Line
                type="monotone"
                dataKey="memory"
                name={t("design.chart.memory")}
                stroke={SERIES_MEMORY}
                strokeWidth={2}
                dot={false}
              />
            </ComposedChart>
          </ResponsiveContainer>
        </CardContent>
      </Card>

      {/* Состояния: загрузка / пусто / ошибка — статичная демонстрация трёх рядом. */}
      <section className="space-y-3">
        <h3 className="text-base font-medium">{t("design.states.title")}</h3>
        <p className="text-muted-foreground text-xs">{t("design.states.hint")}</p>
        <div className="grid grid-cols-1 gap-4 lg:grid-cols-3">
          {/* Загрузка */}
          <Card>
            <CardHeader>
              <CardTitle className="text-sm font-medium">
                {t("design.states.loading.title")}
              </CardTitle>
            </CardHeader>
            <CardContent className="space-y-3">
              <Skeleton className="h-6 w-3/4" />
              <Skeleton className="h-6 w-full" />
              <Skeleton className="h-6 w-2/3" />
            </CardContent>
          </Card>

          {/* Пусто */}
          <Card>
            <CardContent className="flex flex-col items-center justify-center gap-2 py-10 text-center">
              <DatabaseIcon className="text-muted-foreground size-8" aria-hidden="true" />
              <p className="text-sm font-medium">{t("design.states.empty.title")}</p>
              <p className="text-muted-foreground text-xs">{t("design.states.empty.hint")}</p>
            </CardContent>
          </Card>

          {/* Ошибка */}
          <Card>
            <CardHeader>
              <CardTitle className="text-sm font-medium">
                {t("design.states.error.title")}
              </CardTitle>
            </CardHeader>
            <CardContent>
              <div className="border-destructive/40 bg-destructive/5 rounded-md border p-4 text-sm">
                <p className="font-medium">{t("design.states.error.title")}</p>
                <Button variant="link" className="h-auto p-0">
                  {t("design.states.error.retry")}
                </Button>
              </div>
            </CardContent>
          </Card>
        </div>
      </section>

      {/* Полоса квоты лицензий — пример прогресс-бара с цветом по severity. */}
      <Card>
        <CardHeader>
          <CardTitle className="text-base font-medium">{t("design.quota.title")}</CardTitle>
        </CardHeader>
        <CardContent className="space-y-3">
          <p className="text-muted-foreground text-xs">{t("design.quota.hint")}</p>
          <div className="flex items-center justify-between text-sm">
            <span className="font-mono tabular-nums">
              {t("design.quota.label", { consumed: 280, limit: 320 })}
            </span>
            <span className="font-mono tabular-nums">{quota.percent}%</span>
          </div>
          <Progress value={quota.percent} className={quota.progressClass} />
        </CardContent>
      </Card>
    </div>
  );
}
