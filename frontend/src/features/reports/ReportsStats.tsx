import { format } from "date-fns";
import { ru } from "date-fns/locale";
import { useTranslation } from "react-i18next";
import { StatusBadge } from "@/components/ui/StatusBadge";
import { quotaDisplay } from "@/lib/quota";
import type { LicenseUsageSeriesResponse } from "./types";

/** Текстовые пояснения к ряду: пик (с долей от лимита и моментом) и среднее.
 *  Используется и в сводке, и в детализации (контракт одинаков).
 *  MLC-122 / R6: пик окрашивается по той же шкале quotaSeverity (75/90). */
export function ReportsStats({ data }: { data: LicenseUsageSeriesResponse }) {
  const { t } = useTranslation();

  const { percent, severity, badgeVariant } = quotaDisplay(data.peakConsumed, data.peakLimit);
  const average = Math.round(data.averageConsumed * 10) / 10;
  const peakAt = data.peakAtUtc
    ? format(new Date(data.peakAtUtc), "dd.MM.yyyy HH:mm", { locale: ru })
    : null;

  return (
    <div className="flex flex-wrap gap-x-6 gap-y-1 text-sm">
      <div className="flex items-center gap-2">
        <p>
          <span className="text-muted-foreground">{t("reports.stats.peak")}:</span>{" "}
          <span className="font-medium tabular-nums">
            {t("reports.stats.peakValue", {
              consumed: data.peakConsumed,
              limit: data.peakLimit,
              percent,
            })}
          </span>
          {peakAt && <span className="text-muted-foreground"> ({peakAt})</span>}
        </p>
        {severity !== "ok" && (
          <StatusBadge variant={badgeVariant}>
            {severity === "danger" ? t("common.quota.exceeded") : t("common.quota.nearLimit")}
          </StatusBadge>
        )}
      </div>
      <p>
        <span className="text-muted-foreground">{t("reports.stats.average")}:</span>{" "}
        <span className="font-medium tabular-nums">{average}</span>
      </p>
    </div>
  );
}
