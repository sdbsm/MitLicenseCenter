import { useMemo } from "react";
import { InfoIcon } from "lucide-react";
import { useTranslation } from "react-i18next";
import { Bar, BarChart, Cell, ResponsiveContainer, Tooltip, XAxis, YAxis } from "recharts";
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from "@/components/ui/table";
import {
  Tooltip as UITooltip,
  TooltipContent,
  TooltipProvider,
  TooltipTrigger,
} from "@/components/ui/tooltip";
import type { FamilyShare } from "./attribution";
import { familyColor, familyLabel } from "./familyColors";

interface ProcessFamilyAttributionProps {
  families: FamilyShare[];
  // Первый poll: CPU% по процессам ещё не готов (дельта) → CPU-разрез скрыт,
  // RAM (мгновенные рабочие наборы) показывается всегда.
  measuring: boolean;
}

/**
 * Уровень 2 «кто потребляет» (ADR-26): доли семей процессов в CPU и RAM.
 * Горизонтальный stacked-bar (recharts, паттерн `features/reports`) + плотная
 * таблица абсолютных значений. Семьи приходят ключами — локализуются на месте.
 */
export function ProcessFamilyAttribution({ families, measuring }: ProcessFamilyAttributionProps) {
  const { t } = useTranslation();

  // Один ряд = один разрез (RAM всегда, CPU — когда дельта готова). Семья = стек-сегмент.
  const chartData = useMemo(() => {
    const ram: Record<string, number | string> = { dimension: t("performance.attribution.ram") };
    for (const f of families) ram[f.family] = Math.round(f.ramShare * 1000) / 10;
    const rows: Array<Record<string, number | string>> = [];
    if (!measuring) {
      const cpu: Record<string, number | string> = {
        dimension: t("performance.attribution.cpu"),
      };
      for (const f of families) cpu[f.family] = Math.round(f.cpuShare * 1000) / 10;
      rows.push(cpu);
    }
    rows.push(ram);
    return rows;
  }, [families, measuring, t]);

  const hasData = families.length > 0;

  return (
    <div className="space-y-4">
      {hasData ? (
        <ResponsiveContainer width="100%" height={measuring ? 90 : 130}>
          <BarChart
            data={chartData}
            layout="vertical"
            stackOffset="expand"
            margin={{ top: 4, right: 8, bottom: 4, left: 8 }}
          >
            <XAxis type="number" hide domain={[0, 1]} />
            <YAxis
              type="category"
              dataKey="dimension"
              width={48}
              tick={{ fontSize: 12 }}
              className="text-muted-foreground"
            />
            <Tooltip
              contentStyle={{ fontSize: 12 }}
              formatter={(value, name) => [
                t("performance.units.percent", { value: Number(value) }),
                familyLabel(t, String(name)),
              ]}
            />
            {families.map((f) => (
              <Bar key={f.family} dataKey={f.family} stackId="s" fill={familyColor(f.family)}>
                <Cell fill={familyColor(f.family)} />
              </Bar>
            ))}
          </BarChart>
        </ResponsiveContainer>
      ) : (
        <p className="text-muted-foreground text-sm">{t("performance.attribution.empty")}</p>
      )}

      {hasData && (
        <Table>
          <TableHeader>
            <TableRow>
              <TableHead>{t("performance.attribution.family")}</TableHead>
              <TableHead className="text-right">
                <TooltipProvider delayDuration={150}>
                  <UITooltip>
                    <TooltipTrigger asChild>
                      <span className="inline-flex cursor-help items-center justify-end gap-1">
                        {t("performance.attribution.cpu")}
                        <InfoIcon className="text-muted-foreground size-3.5" />
                      </span>
                    </TooltipTrigger>
                    <TooltipContent className="max-w-xs">
                      {t("performance.attribution.cpuAveragedTooltip")}
                    </TooltipContent>
                  </UITooltip>
                </TooltipProvider>
              </TableHead>
              <TableHead className="text-right">{t("performance.attribution.ram")}</TableHead>
              <TableHead className="text-right">{t("performance.attribution.processes")}</TableHead>
            </TableRow>
          </TableHeader>
          <TableBody>
            {families.map((f) => (
              <TableRow key={f.family}>
                <TableCell>
                  <span className="flex items-center gap-2">
                    <span
                      className="inline-block h-2.5 w-2.5 shrink-0 rounded-sm"
                      style={{ backgroundColor: familyColor(f.family) }}
                    />
                    {familyLabel(t, f.family)}
                  </span>
                </TableCell>
                <TableCell className="text-right tabular-nums">
                  {measuring ? (
                    <span className="text-muted-foreground">{t("performance.measuring")}</span>
                  ) : (
                    t("performance.units.percent", { value: Math.round(f.cpuPercent) })
                  )}
                </TableCell>
                <TableCell className="text-right tabular-nums">
                  {t("performance.units.gb", { value: round1(f.ramBytes / 1024 ** 3) })}
                </TableCell>
                <TableCell className="text-muted-foreground text-right tabular-nums">
                  {f.processCount}
                </TableCell>
              </TableRow>
            ))}
          </TableBody>
        </Table>
      )}
    </div>
  );
}

function round1(n: number): number {
  return Math.round(n * 10) / 10;
}
