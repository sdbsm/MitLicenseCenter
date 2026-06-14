import { useTranslation } from "react-i18next";
import { Pause, Play, RefreshCw } from "lucide-react";
import { Button } from "@/components/ui/button";
import { cn } from "@/lib/utils";

interface LiveControlsProps {
  /** Авто-обновление таблицы заморожено. */
  isPaused: boolean;
  /** Переключить паузу авто-обновления. */
  onTogglePause: () => void;
  /** Обновить данные немедленно (на /sessions — живой форс-обход 1С). */
  onRefreshNow: () => void;
  /** Идёт ручное обновление — крутим иконку и блокируем повторный клик. */
  isRefreshing?: boolean;
}

// MLC-156: переиспользуемая пара контролов для «живых» страниц (/sessions,
// /performance): Пауза/Возобновить авто-обновления таблицы + «Обновить сейчас».
// На /sessions «Обновить сейчас» = живой форс-обход 1С (POST /sessions/refresh),
// на /performance = refetch активной вкладки.
export function LiveControls({
  isPaused,
  onTogglePause,
  onRefreshNow,
  isRefreshing = false,
}: LiveControlsProps) {
  const { t } = useTranslation();

  const pauseLabel = isPaused ? t("common.resume") : t("common.pause");

  return (
    <div className="flex items-center gap-2">
      <Button
        variant="outline"
        size="sm"
        onClick={onTogglePause}
        aria-label={pauseLabel}
        title={pauseLabel}
      >
        {isPaused ? <Play aria-hidden="true" /> : <Pause aria-hidden="true" />}
        {pauseLabel}
      </Button>
      <Button
        variant="outline"
        size="sm"
        onClick={onRefreshNow}
        disabled={isRefreshing}
        aria-label={t("common.refreshNow")}
        title={t("common.refreshNow")}
      >
        <RefreshCw aria-hidden="true" className={cn(isRefreshing && "animate-spin")} />
        {t("common.refreshNow")}
      </Button>
    </div>
  );
}
