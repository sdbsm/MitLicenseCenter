import { useTranslation } from "react-i18next";
import { cn } from "@/lib/utils";
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from "@/components/ui/table";
import { Tooltip, TooltipContent, TooltipTrigger } from "@/components/ui/tooltip";
import { availablePerformanceBand, formatAvgCallMs, formatBytes, shortUuid } from "./onecLoad";
import type { OneCProcessLoad } from "./types";

interface OneCProcessesTableProps {
  processes: OneCProcessLoad[];
}

const PERF_TINT: Record<"ok" | "warn" | "crit", string> = {
  ok: "text-emerald-700 dark:text-emerald-300",
  warn: "text-amber-700 dark:text-amber-300",
  crit: "text-rose-700 dark:text-rose-300",
};

/**
 * Рабочие процессы кластера 1С (`rphost`, MLC-067) — `available-perfomance` (↓ = деградация),
 * средняя длительность вызова, память, pid. Тинт производительности подсказывает, какой
 * рабочий процесс «просел». Отсутствующие perf-поля — «—», не 0.
 */
export function OneCProcessesTable({ processes }: OneCProcessesTableProps) {
  const { t } = useTranslation();

  return (
    <div className="rounded-md border">
      <Table>
        <TableHeader>
          <TableRow>
            <TableHead className="w-24">{t("performance.onec.processes.process")}</TableHead>
            <TableHead className="w-20 text-right">{t("performance.onec.processes.pid")}</TableHead>
            <TableHead className="text-right">
              {t("performance.onec.processes.availablePerformance")}
            </TableHead>
            <TableHead className="w-28 text-right">
              {t("performance.onec.processes.avgCallTime")}
            </TableHead>
            <TableHead className="w-28 text-right">
              {t("performance.onec.processes.memory")}
            </TableHead>
          </TableRow>
        </TableHeader>
        <TableBody>
          {processes.length === 0 ? (
            <TableRow>
              <TableCell colSpan={5} className="text-muted-foreground py-8 text-center text-sm">
                {t("performance.onec.processes.empty")}
              </TableCell>
            </TableRow>
          ) : (
            processes.map((p) => {
              const band = availablePerformanceBand(p.availablePerformance);
              return (
                <TableRow key={p.process}>
                  <TableCell>
                    <Tooltip>
                      <TooltipTrigger asChild>
                        <span className="cursor-help font-mono text-xs">
                          {shortUuid(p.process)}
                        </span>
                      </TooltipTrigger>
                      <TooltipContent>
                        <span className="font-mono text-xs">{p.process}</span>
                      </TooltipContent>
                    </Tooltip>
                  </TableCell>
                  <TableCell className="text-muted-foreground text-right font-mono text-xs tabular-nums">
                    {p.pid ?? "—"}
                  </TableCell>
                  <TableCell
                    className={cn(
                      "text-right font-medium tabular-nums",
                      band ? PERF_TINT[band] : "text-muted-foreground"
                    )}
                  >
                    {p.availablePerformance ?? "—"}
                  </TableCell>
                  <TableCell className="text-right tabular-nums">
                    {formatAvgCallMs(p.avgCallTime)}
                  </TableCell>
                  <TableCell className="text-right tabular-nums">
                    {formatBytes(p.memorySize)}
                  </TableCell>
                </TableRow>
              );
            })
          )}
        </TableBody>
      </Table>
    </div>
  );
}
