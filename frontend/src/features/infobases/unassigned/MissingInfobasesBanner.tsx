import { AlertTriangleIcon } from "lucide-react";
import { useTranslation } from "react-i18next";
import { Button } from "@/components/ui/button";
import { RelativeTime } from "@/components/ui/RelativeTime";

interface MissingInfobasesBannerProps {
  count: number;
  checkedAtUtc: string;
  onShow: () => void;
}

/**
 * MLC-096 — красный баннер обратного дрейфа: записи панели, чьего UUID нет в кластере 1С
 * («мёртвые души» — база удалена/пересоздана, а в панели висит здоровой). Семантика
 * `danger` (rose, 06 §3 — Drift), иконка `AlertTriangle` (06 §10). Слот рядом с жёлтым
 * баннером нераспределённых: один и тот же query, без второго запроса. Родитель рендерит
 * строго при `available && count > 0` (при недоступном RAS меток и баннера нет — сбой
 * опроса ≠ пропавшие базы). Свежесть — идиома 06 §8 (`<RelativeTime>`, точное время в тултипе).
 */
export function MissingInfobasesBanner({
  count,
  checkedAtUtc,
  onShow,
}: MissingInfobasesBannerProps) {
  const { t } = useTranslation();

  return (
    <div className="flex flex-wrap items-center gap-3 rounded-md border border-rose-600/40 bg-rose-600/5 p-3 text-sm text-rose-700 dark:text-rose-300">
      <AlertTriangleIcon className="size-5 shrink-0" />
      <span className="font-medium">{t("infobases.missing.banner.text", { count })}</span>
      <span className="text-muted-foreground text-xs">
        {t("infobases.unassigned.checkedAt")} <RelativeTime value={checkedAtUtc} />
      </span>
      <div className="ml-auto flex items-center gap-2">
        <Button size="sm" variant="outline" onClick={onShow}>
          {t("infobases.missing.banner.show")}
        </Button>
      </div>
    </div>
  );
}
