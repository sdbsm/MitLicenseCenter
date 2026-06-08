import { useTranslation } from "react-i18next";
import { Progress } from "@/components/ui/progress";
import { cn } from "@/lib/utils";
import type { Saturation } from "./thresholds";

// Тинт индикатора `Progress` по сатурации (06_UI_DESIGN §3: success/warning/danger).
// Полные строки классов статичны (JIT Tailwind их видит); целимся в дочерний
// indicator через arbitrary-variant, т.к. className уходит на Root.
const BAR_CLASS: Record<Saturation, string> = {
  ok: "[&_[data-slot=progress-indicator]]:bg-emerald-600 dark:[&_[data-slot=progress-indicator]]:bg-emerald-500",
  warn: "[&_[data-slot=progress-indicator]]:bg-amber-600 dark:[&_[data-slot=progress-indicator]]:bg-amber-500",
  crit: "[&_[data-slot=progress-indicator]]:bg-red-600 dark:[&_[data-slot=progress-indicator]]:bg-red-500",
};

const VALUE_CLASS: Record<Saturation, string> = {
  ok: "text-emerald-600 dark:text-emerald-400",
  warn: "text-amber-600 dark:text-amber-400",
  crit: "text-red-600 dark:text-red-400",
};

interface MetricGaugeProps {
  label: string;
  // Читаемое значение (например «42 %» или «18 мс»); скрыто при measuring.
  valueText: string;
  // Заполнение шкалы 0–100.
  fillPercent: number;
  saturation: Saturation;
  // Подпись-пояснение под шкалой (сигнал насыщения: очередь / paging / латентность).
  detail?: string;
  // Первый poll: дельта-метрика ещё не готова — рисуем «измеряю…», не ноль.
  measuring?: boolean;
}

/**
 * Радиального gauge в наборе нет — гейдж строится на линейном `Progress` (решение
 * трека). Переиспользуемый примитив «по месту» (обобщение в Фазе 5). Цвет шкалы и
 * значения отражают сатурацию (светофор ADR-26), а не голый процент.
 */
export function MetricGauge({
  label,
  valueText,
  fillPercent,
  saturation,
  detail,
  measuring = false,
}: MetricGaugeProps) {
  const { t } = useTranslation();

  return (
    <div className="space-y-2">
      <div className="flex items-baseline justify-between gap-2">
        <span className="text-muted-foreground text-sm font-medium">{label}</span>
        {measuring ? (
          <span className="text-muted-foreground animate-pulse text-sm">
            {t("performance.measuring")}
          </span>
        ) : (
          <span className={cn("text-sm font-semibold tabular-nums", VALUE_CLASS[saturation])}>
            {valueText}
          </span>
        )}
      </div>
      <Progress
        value={measuring ? 0 : Math.min(100, Math.max(0, fillPercent))}
        className={cn("h-2.5", !measuring && BAR_CLASS[saturation], measuring && "animate-pulse")}
      />
      <p className="text-muted-foreground min-h-4 text-xs">{measuring ? "" : detail}</p>
    </div>
  );
}
