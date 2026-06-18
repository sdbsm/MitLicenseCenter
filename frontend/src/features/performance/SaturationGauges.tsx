import { InfoIcon } from "lucide-react";
import { useTranslation } from "react-i18next";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Tooltip, TooltipContent, TooltipProvider, TooltipTrigger } from "@/components/ui/tooltip";
import { MetricGauge } from "./MetricGauge";
import {
  cpuSaturation,
  diskFillPercent,
  diskMaxLatencySec,
  diskSaturation,
  ramSaturation,
  ramUsedPercent,
} from "./thresholds";
import type { HostMetricsSnapshot } from "./types";

interface SaturationGaugesProps {
  snapshot: HostMetricsSnapshot;
  // Когда задан — гейджи кликабельны: клик по ресурсу наводит drill-down на слой
  // (Срез B). Не задан — гейджи неинтерактивны (прежнее поведение).
  onResourceClick?: (resource: "cpu" | "ram" | "disk") => void;
}

/**
 * Уровень 1 «светофор ресурсов» (ADR-26): три гейджа CPU/RAM/Disk. CPU и Disk
 * зависят от дельты двух замеров → на первом poll'е (measuring) показывают
 * «измеряю…». RAM мгновенный (available/total) — рисуется всегда, даже на первом poll'е.
 */
export function SaturationGauges({ snapshot, onResourceClick }: SaturationGaugesProps) {
  const { t } = useTranslation();
  const { measuring, cpu, memory, disk } = snapshot;

  const ramUsed = ramUsedPercent(memory);
  const diskMs = Math.round(diskMaxLatencySec(disk) * 1000);

  // aria-подпись кнопки-гейджа: «Открыть слой расследования для ресурса …» с
  // локализованным именем ресурса (без хардкода в TSX). Только когда клик включён.
  const drilldownAria = (resource: "cpu" | "ram" | "disk"): string | undefined =>
    onResourceClick
      ? t("performance.saturation.drilldownAria", {
          resource: t(`performance.resources.${resource}`),
        })
      : undefined;

  const cpuLabel = (
    <TooltipProvider delayDuration={150}>
      <Tooltip>
        <TooltipTrigger asChild>
          <span className="inline-flex cursor-help items-center gap-1">
            {t("performance.saturation.cpu")}
            <InfoIcon className="text-muted-foreground size-3.5" />
          </span>
        </TooltipTrigger>
        <TooltipContent className="max-w-xs">
          {t("performance.saturation.cpuInstantTooltip")}
        </TooltipContent>
      </Tooltip>
    </TooltipProvider>
  );

  return (
    <Card>
      <CardHeader>
        <CardTitle>{t("performance.saturation.title")}</CardTitle>
      </CardHeader>
      <CardContent className="grid grid-cols-1 gap-x-8 gap-y-6 sm:grid-cols-3">
        <MetricGauge
          label={cpuLabel}
          valueText={t("performance.units.percent", { value: Math.round(cpu.totalPercent) })}
          fillPercent={cpu.totalPercent}
          saturation={cpuSaturation(cpu)}
          detail={t("performance.saturation.cpuDetail", { queue: round1(cpu.queueLength) })}
          measuring={measuring}
          onClick={onResourceClick ? () => onResourceClick("cpu") : undefined}
          ariaLabel={drilldownAria("cpu")}
        />
        <MetricGauge
          label={t("performance.saturation.ram")}
          valueText={t("performance.units.percent", { value: Math.round(ramUsed) })}
          fillPercent={ramUsed}
          saturation={ramSaturation(memory)}
          detail={t("performance.saturation.ramDetail", {
            availableGb: round1(memory.availableMBytes / 1024),
            totalGb: round1(memory.totalMBytes / 1024),
            pages: Math.round(memory.pagesPerSec),
          })}
          // RAM-занятость мгновенна; paging — дельта, но занятость честно рисуется и на 1-м poll'е.
          measuring={false}
          onClick={onResourceClick ? () => onResourceClick("ram") : undefined}
          ariaLabel={drilldownAria("ram")}
        />
        <MetricGauge
          label={t("performance.saturation.disk")}
          valueText={t("performance.units.ms", { value: diskMs })}
          fillPercent={diskFillPercent(disk)}
          saturation={diskSaturation(disk)}
          detail={t("performance.saturation.diskDetail", { queue: round1(disk.queueLength) })}
          measuring={measuring}
          onClick={onResourceClick ? () => onResourceClick("disk") : undefined}
          ariaLabel={drilldownAria("disk")}
        />
      </CardContent>
    </Card>
  );
}

function round1(n: number): number {
  return Math.round(n * 10) / 10;
}
