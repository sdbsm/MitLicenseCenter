import { useTranslation } from "react-i18next";
import { toast } from "sonner";
import { CircleIcon, CircleStopIcon, ShieldCheckIcon } from "lucide-react";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { useMe } from "@/features/auth/useAuth";
import { useInfobases } from "@/features/infobases/useInfobases";
import { matchConflictCode } from "@/lib/apiErrors";
import {
  type InvestigationSummary,
  type InvestigationProgress as ProgressData,
} from "@/features/investigations/types";
import { useStopInvestigation } from "@/features/investigations/useInvestigations";

/**
 * Прогресс активного сбора — экран 6 (MLC-242, ADR-57, спека §Экран 6).
 *
 * Показывается, пока активное дело в статусе Collecting или Analyzing.
 * Статус Collecting: прошедшее время, собранный объём, кнопка «Остановить сейчас» (Admin).
 * Статус Analyzing: сбор снят, идёт разбор — кнопка стоп недоступна.
 * Полноценная карточка «Дело» с находками — MLC-243.
 */

interface InvestigationProgressProps {
  /** Шапка активного дела (из списка `useInvestigations`). */
  summary: InvestigationSummary;
  /** Лёгкий снимок прогресса (из `useInvestigationProgress`); null = ещё не пришёл. */
  progress: ProgressData | null;
}

/** Форматирует секунды в читаемую строку «X мин Y сек» / «Y сек». */
function formatElapsed(
  t: (key: string, opts?: Record<string, unknown>) => string,
  seconds: number
): string {
  if (seconds < 60) {
    return t("performance.investigation.progress.seconds", { value: seconds });
  }
  const mins = Math.floor(seconds / 60);
  const secs = seconds % 60;
  return t("performance.investigation.progress.minutes", { value: mins, seconds: secs });
}

/** Форматирует байты в КБ или МБ. */
function formatBytes(
  t: (key: string, opts?: Record<string, unknown>) => string,
  bytes: number
): string {
  if (bytes < 1024 * 1024) {
    return t("performance.investigation.progress.kb", { value: Math.round(bytes / 1024) });
  }
  return t("performance.investigation.progress.mb", {
    value: (bytes / (1024 * 1024)).toFixed(1),
  });
}

export function InvestigationProgress({ summary, progress }: InvestigationProgressProps) {
  const { t } = useTranslation();
  const { data: me } = useMe();
  const isAdmin = me?.roles?.includes("Admin") ?? false;

  const stopInvestigation = useStopInvestigation();
  const isCollecting = summary.status === "Collecting";
  const isAnalyzing = summary.status === "Analyzing";

  // Имя привязанной ИБ из реестра (кэш-запрос; полноценная атрибуция арендатора — карточка дела MLC-243).
  const { data: infobasesData } = useInfobases(null, null, false, 1, 100);

  const handleStop = async () => {
    try {
      await stopInvestigation.mutateAsync(summary.id);
      toast.success(t("investigations.status.Interrupted"));
    } catch (error) {
      const conflictKey = matchConflictCode(error, {
        INVESTIGATION_NOT_ACTIVE:
          "performance.investigation.progress.errors.INVESTIGATION_NOT_ACTIVE",
      });
      if (conflictKey) {
        toast.error(t(conflictKey));
        return;
      }
      toast.error(t("errors.generic"));
    }
  };

  // Scope-строка: привязано к ИБ — её имя из реестра (fallback на id, пока список не пришёл), иначе «Весь узел»
  const scopeLabel = summary.infobaseId
    ? (infobasesData?.items.find((ib) => ib.id === summary.infobaseId)?.name ?? summary.infobaseId)
    : t("performance.investigation.progress.scopeAll");

  return (
    <Card>
      <CardHeader>
        <div className="flex flex-wrap items-start justify-between gap-3">
          <div className="space-y-1">
            <div className="flex items-center gap-2">
              {isCollecting && (
                <CircleIcon
                  className="size-3 animate-pulse fill-rose-600 text-rose-600 dark:fill-rose-400 dark:text-rose-400"
                  aria-hidden="true"
                />
              )}
              <CardTitle>
                {isAnalyzing
                  ? t("performance.investigation.progress.titleAnalyzing")
                  : t("performance.investigation.progress.title")}
              </CardTitle>
            </div>
          </div>

          {/* Кнопка «Остановить» — только Admin и только в статусе Collecting */}
          {isAdmin && isCollecting && (
            <Button
              variant="outline"
              size="sm"
              disabled={stopInvestigation.isPending}
              onClick={() => void handleStop()}
            >
              <CircleStopIcon className="size-4" />
              {stopInvestigation.isPending
                ? t("performance.investigation.progress.stopping")
                : t("performance.investigation.progress.stopNow")}
            </Button>
          )}
        </div>
      </CardHeader>

      <CardContent className="space-y-4">
        {/* Сценарий + цель */}
        <dl className="grid grid-cols-[auto_1fr] gap-x-4 gap-y-2 text-sm">
          <dt className="text-muted-foreground">
            {t("performance.investigation.progress.scenario")}
          </dt>
          <dd className="font-medium">{t(`investigations.scenario.${summary.scenario}`)}</dd>

          <dt className="text-muted-foreground">{t("performance.investigation.progress.scope")}</dt>
          <dd className="font-medium">{scopeLabel}</dd>

          {/* Прошедшее время */}
          {progress !== null && (
            <>
              <dt className="text-muted-foreground">
                {t("performance.investigation.progress.elapsed")}
              </dt>
              <dd className="font-medium">{formatElapsed(t, progress.elapsedSeconds)}</dd>
            </>
          )}

          {/* Объём собранного (опускается, если нет) */}
          {progress?.collectedBytes != null && (
            <>
              <dt className="text-muted-foreground">
                {t("performance.investigation.progress.collected")}
              </dt>
              <dd className="font-medium">{formatBytes(t, progress.collectedBytes)}</dd>
            </>
          )}
        </dl>

        {/* Hint для Analyzing */}
        {isAnalyzing && (
          <p className="text-muted-foreground text-sm">
            {t("performance.investigation.progress.analyzingHint")}
          </p>
        )}

        {/* Гарантия авто-снятия */}
        <div className="bg-muted/60 flex gap-3 rounded-lg p-3">
          <ShieldCheckIcon
            className="text-muted-foreground mt-0.5 size-4 shrink-0"
            aria-hidden="true"
          />
          <p className="text-muted-foreground text-xs leading-relaxed">
            {t("performance.investigation.progress.guarantee")}
          </p>
        </div>
      </CardContent>
    </Card>
  );
}
