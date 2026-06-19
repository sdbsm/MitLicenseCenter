import { useState } from "react";
import { useTranslation } from "react-i18next";
import { Button } from "@/components/ui/button";
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
import { useMe } from "@/features/auth/useAuth";
import {
  availablePerformanceBand,
  formatAvgCallMs,
  formatBytes,
  shortUuid,
} from "@/features/performance/onecLoad";
import { OneCProcessRestartDialog } from "./OneCProcessRestartDialog";
import { useOneCProcesses, type OneCProcess } from "./useOneCProcesses";

/**
 * Блок «Рабочие процессы 1С» (`rphost`) вкладки «Службы» раздела «Сервер» (MLC-219/220,
 * ADR-54/56). Таблица live-снимка процессов кластера: UUID процесса, PID, доступная
 * производительность (`available-perfomance`, ↓ = деградация — подсвечивается тинтом), средняя
 * длительность вызова, память. Отсутствующие perf-поля — «—», не 0. Пустой список = rac
 * недоступен/не настроен (best-effort, экран не падает).
 *
 * Admin (роль-гейт как у OneCServerActionDialog) видит колонку «Действия» с кнопкой
 * «Перезапустить» в строке — мягкий рестарт rphost по Pid через confirm-диалог (MLC-220):
 * у rac нет «restart process» → рестарт = завершение ОС-процесса rphost, кластер 1С поднимает
 * новый. Кнопка недоступна, если Pid не пришёл (perf-поле опущено). Viewer — только чтение.
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
  const { data: me } = useMe();
  const isAdmin = me?.roles?.includes("Admin") ?? false;

  // Колонка действий — только для Admin (рестарт rphost). colSpan пустого ряда учитывает её.
  const columnCount = isAdmin ? 6 : 5;

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
              {isAdmin && (
                <TableHead className="w-32 text-right">
                  {t("server.processes.columns.actions")}
                </TableHead>
              )}
            </TableRow>
          </TableHeader>
          <TableBody>
            {processes.length === 0 ? (
              <TableRow>
                <TableCell
                  colSpan={columnCount}
                  className="text-muted-foreground py-8 text-center text-sm"
                >
                  {t("server.processes.empty")}
                </TableCell>
              </TableRow>
            ) : (
              processes.map((p) => <ProcessRow key={p.process} process={p} isAdmin={isAdmin} />)
            )}
          </TableBody>
        </Table>
      </div>
    </TooltipProvider>
  );
}

function ProcessRow({ process: p, isAdmin }: { process: OneCProcess; isAdmin: boolean }) {
  const { t } = useTranslation();
  const [restartOpen, setRestartOpen] = useState(false);
  const band = availablePerformanceBand(p.availablePerformance);

  return (
    <TableRow>
      <TableCell>
        <Tooltip>
          <TooltipTrigger asChild>
            <span className="cursor-help font-mono text-xs">{shortUuid(p.process)}</span>
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
      <TableCell className="text-right tabular-nums">{formatAvgCallMs(p.avgCallTime)}</TableCell>
      <TableCell className="text-right tabular-nums">{formatBytes(p.memorySize)}</TableCell>
      {isAdmin && (
        <TableCell className="text-right">
          {/* Рестарт доступен только при наличии Pid (whitelist/guard работают по Pid). */}
          {p.pid == null ? (
            <span className="text-muted-foreground text-xs">
              {t("server.processes.restart.noPid")}
            </span>
          ) : (
            <Button size="sm" variant="outline" onClick={() => setRestartOpen(true)}>
              {t("server.processes.restart.action")}
            </Button>
          )}
          {p.pid != null && (
            <OneCProcessRestartDialog
              open={restartOpen}
              onOpenChange={setRestartOpen}
              pid={p.pid}
            />
          )}
        </TableCell>
      )}
    </TableRow>
  );
}
