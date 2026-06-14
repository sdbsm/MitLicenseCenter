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
//
// MLC-158: цвет кодирует ТЕКУЩЕЕ состояние, не действие. Слева — индикатор статуса:
// зелёный «● Авто-обновление» (живёт) либо янтарная плашка «⏸ На паузе» (заморожено).
// На паузе кнопка «Возобновить» зелёная — понятный призыв «вернуть в живой режим»;
// в живом режиме «Пауза» нейтральная (янтарный не пугает, когда всё работает).
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
      {isPaused ? (
        <span className="inline-flex items-center gap-1.5 rounded-md bg-amber-100 px-2 py-0.5 text-xs font-medium text-amber-700 dark:bg-amber-950 dark:text-amber-400">
          <Pause className="size-3" aria-hidden="true" />
          {t("common.pausedStatus")}
        </span>
      ) : (
        <span className="text-muted-foreground inline-flex items-center gap-1.5 text-xs font-medium">
          <span className="size-2 rounded-full bg-emerald-500" aria-hidden="true" />
          {t("common.live")}
        </span>
      )}
      <Button
        variant="outline"
        size="sm"
        onClick={onTogglePause}
        className={cn(
          isPaused &&
            "border-emerald-500 text-emerald-700 hover:text-emerald-700 dark:border-emerald-600 dark:text-emerald-400 dark:hover:text-emerald-400"
        )}
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
