import { useTranslation } from "react-i18next";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Skeleton } from "@/components/ui/skeleton";
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from "@/components/ui/table";
import { Tooltip, TooltipContent, TooltipProvider, TooltipTrigger } from "@/components/ui/tooltip";
import { cn } from "@/lib/utils";
import {
  availablePerformanceBand,
  formatAvgCallMs,
  formatBytes,
  shortUuid,
} from "@/features/performance/onecLoad";
import { useOneCProcesses, type OneCProcess } from "./useOneCProcesses";

/**
 * Блок «Рабочие процессы 1С» (`rphost`) вкладки «Службы» раздела «Сервер» (MLC-219, ADR-54).
 * Таблица live-снимка процессов кластера: UUID процесса, PID, доступная производительность
 * (`available-perfomance`, ↓ = деградация — подсвечивается тинтом), средняя длительность вызова,
 * память. Только чтение (Viewer/Admin) — рестарт не реализован (исследовательская часть отложена,
 * см. отчёт MLC-219). Отсутствующие perf-поля — «—», не 0. Пустой список = rac недоступен/не
 * настроен (best-effort, экран не падает).
 *
 * Форматтеры и тинт переиспользуются из «Быстродействия» (features/performance/onecLoad) — тот
 * же источник `rac process list`, одна логика отображения.
 */
const PERF_TINT: Record<"ok" | "warn" | "crit", string> = {
  ok: "text-emerald-700 dark:text-emerald-300",
  warn: "text-amber-700 dark:text-amber-300",
  crit: "text-rose-700 dark:text-rose-300",
};

export function OneCProcessesCard() {
  const { t } = useTranslation();
  const { data, isLoading, isError } = useOneCProcesses();

  return (
    <Card>
      <CardHeader>
        <CardTitle>{t("server.processes.title")}</CardTitle>
        <p className="text-muted-foreground text-sm">{t("server.processes.description")}</p>
      </CardHeader>
      <CardContent>
        {isError && (
          <p className="text-status-danger text-sm">{t("server.processes.loadFailed")}</p>
        )}

        {isLoading && !data && <Skeleton className="h-32 w-full" />}

        {data && <ProcessesTable processes={data.processes} />}
      </CardContent>
    </Card>
  );
}

function ProcessesTable({ processes }: { processes: OneCProcess[] }) {
  const { t } = useTranslation();

  return (
    <TooltipProvider>
      <div className="rounded-md border">
        <Table>
          <TableHeader>
            <TableRow>
              <TableHead className="w-24">{t("server.processes.columns.process")}</TableHead>
              <TableHead className="w-20 text-right">{t("server.processes.columns.pid")}</TableHead>
              <TableHead className="text-right">
                {t("server.processes.columns.availablePerformance")}
              </TableHead>
              <TableHead className="w-28 text-right">
                {t("server.processes.columns.avgCallTime")}
              </TableHead>
              <TableHead className="w-28 text-right">
                {t("server.processes.columns.memory")}
              </TableHead>
            </TableRow>
          </TableHeader>
          <TableBody>
            {processes.length === 0 ? (
              <TableRow>
                <TableCell colSpan={5} className="text-muted-foreground py-8 text-center text-sm">
                  {t("server.processes.empty")}
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
    </TooltipProvider>
  );
}
