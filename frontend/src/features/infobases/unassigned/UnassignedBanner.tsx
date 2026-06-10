import { AlertTriangleIcon } from "lucide-react";
import { useTranslation } from "react-i18next";
import { Button } from "@/components/ui/button";
import { RelativeTime } from "@/components/ui/RelativeTime";

interface UnassignedBannerProps {
  count: number;
  checkedAtUtc: string;
  onRefresh: () => void;
  onResolve: () => void;
  isRefreshing: boolean;
}

/**
 * MLC-093 — баннер-счётчик нераспределённых баз кластера на вкладке «Базы». Семантика
 * `warning` (amber, 06 §3): базы есть в кластере 1С, но не заведены — их сеансы не считаются
 * в лимиты. Рендерится родителем строго при `available && count > 0` (ложного нуля не
 * показываем); сам компонент допускает count>0. Свежесть — идиома 06 §8 (`<RelativeTime>`,
 * точное время в тултипе).
 */
export function UnassignedBanner({
  count,
  checkedAtUtc,
  onRefresh,
  onResolve,
  isRefreshing,
}: UnassignedBannerProps) {
  const { t } = useTranslation();

  return (
    <div className="flex flex-wrap items-center gap-3 rounded-md border border-amber-600/40 bg-amber-600/5 p-3 text-sm text-amber-700 dark:text-amber-300">
      <AlertTriangleIcon className="size-5 shrink-0" />
      <span className="font-medium">{t("infobases.unassigned.banner.text", { count })}</span>
      <span className="text-muted-foreground text-xs">
        {t("infobases.unassigned.checkedAt")} <RelativeTime value={checkedAtUtc} />
      </span>
      <div className="ml-auto flex items-center gap-2">
        <Button variant="ghost" size="sm" onClick={onRefresh} disabled={isRefreshing}>
          {t("common.refresh")}
        </Button>
        <Button size="sm" onClick={onResolve}>
          {t("infobases.unassigned.banner.resolve")}
        </Button>
      </div>
    </div>
  );
}
