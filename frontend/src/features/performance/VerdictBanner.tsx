import { ActivityIcon, CircleCheckIcon, TriangleAlertIcon } from "lucide-react";
import { useTranslation } from "react-i18next";
import { cn } from "@/lib/utils";
import type { Verdict } from "./attribution";
import { familyLabel } from "./familyColors";

interface VerdictBannerProps {
  verdict: Verdict;
}

/**
 * Сводный баннер «почему тормозит» — прямой ответ раздела. Цвет/иконка по уровню
 * (06_UI_DESIGN §3). Текст связывает насыщенный ресурс с главным потребителем
 * (атрибуция); на первом poll'е — нейтральное «идёт измерение».
 */
export function VerdictBanner({ verdict }: VerdictBannerProps) {
  const { t } = useTranslation();
  const { level, resource, culpritFamily } = verdict;

  const tone =
    level === "crit"
      ? "border-red-600/40 bg-red-600/5 text-red-700 dark:text-red-300"
      : level === "warn"
        ? "border-amber-600/40 bg-amber-600/5 text-amber-700 dark:text-amber-300"
        : level === "measuring"
          ? "border-border bg-muted/30 text-muted-foreground"
          : "border-emerald-600/40 bg-emerald-600/5 text-emerald-700 dark:text-emerald-300";

  const Icon =
    level === "crit" || level === "warn"
      ? TriangleAlertIcon
      : level === "measuring"
        ? ActivityIcon
        : CircleCheckIcon;

  const message = (() => {
    if (level === "measuring") return t("performance.verdict.measuring");
    if (level === "ok") return t("performance.verdict.ok");
    // warn/crit: ресурс-узкое-место (+ потребитель, если это CPU/RAM).
    const resourceName = t(`performance.resources.${resource}`);
    if (culpritFamily) {
      return t("performance.verdict.bottleneckCulprit", {
        resource: resourceName,
        family: familyLabel(t, culpritFamily),
      });
    }
    return t("performance.verdict.bottleneck", { resource: resourceName });
  })();

  return (
    <div
      className={cn(
        "flex items-center gap-3 rounded-md border p-3 text-sm font-medium",
        tone,
        level === "measuring" && "animate-pulse"
      )}
    >
      <Icon className="h-5 w-5 shrink-0" />
      <span>{message}</span>
    </div>
  );
}
