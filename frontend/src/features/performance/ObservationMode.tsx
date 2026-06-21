import { useTranslation } from "react-i18next";
import { toast } from "sonner";
import { LiveControls } from "@/components/LiveControls";
import { RelativeTime } from "@/components/ui/RelativeTime";
import { Button } from "@/components/ui/button";
import { Skeleton } from "@/components/ui/skeleton";
import { AttributionWarningBanner } from "./AttributionWarningBanner";
import { PerformanceBlockingBlock } from "./PerformanceBlockingBlock";
import { PerformanceDrillDown } from "./PerformanceDrillDown";
import { RecordingSection } from "./RecordingSection";
import { SaturationGauges } from "./SaturationGauges";
import { VerdictBanner } from "./VerdictBanner";
import { layerForResource, useDrillDownFocus } from "./useDrillDownFocus";
import { usePerformancePage } from "./usePerformancePage";

interface ObservationModeProps {
  /** Колбэк «начать расследование» — переключает родительский режим на investigation. */
  onStartInvestigation: () => void;
}

/**
 * Режим «Наблюдение» в разделе «Производительность» (MLC-241, ADR-57).
 * Вся нынешняя live-панель: вердикт → светофор → drill-down (Хост/1С/SQL) →
 * блокировки → запись по требованию.
 *
 * Вынесена из PerformancePage без изменения поведения: polling, авто-фокус
 * useDrillDownFocus, пиннинг, forceMount фоновых слоёв, LiveControls — всё как было.
 * LiveControls и индикатор свежести рендерятся только в этом режиме
 * (в «Расследовании» live-управление неуместно).
 */
export function ObservationMode({ onStartInvestigation }: ObservationModeProps) {
  const { t } = useTranslation();
  const {
    snapshot,
    measuring,
    derived,
    isLoading,
    isError,
    refetch,
    failureCount,
    isPaused,
    togglePause,
    refreshNow,
    isRefreshing,
  } = usePerformancePage();

  const { layer, setLayer } = useDrillDownFocus(derived?.verdict ?? null);

  return (
    <div className="space-y-6">
      {/* Подзаголовок live-режима + свежесть + LiveControls + CTA-мост — только в «Наблюдении» */}
      <div className="flex items-start justify-between gap-4">
        <p className="text-muted-foreground max-w-2xl text-sm">
          {t("performance.observationSubtitle")}
        </p>
        <div className="flex shrink-0 items-center gap-3">
          {snapshot && (
            <span className="text-muted-foreground flex items-center gap-1 text-sm">
              {t("performance.freshness")}{" "}
              <RelativeTime
                value={snapshot.capturedAtUtc}
                thresholdAmberSec={30}
                isError={failureCount >= 2}
              />
            </span>
          )}
          <LiveControls
            isPaused={isPaused}
            onTogglePause={togglePause}
            onRefreshNow={() => void refreshNow()}
            isRefreshing={isRefreshing}
          />
          {/* CTA-мост к расследованию (спека экрана 1, «начать расследование») */}
          <Button variant="outline" size="sm" onClick={onStartInvestigation}>
            {t("performance.startInvestigation")}
          </Button>
        </div>
      </div>

      {/* Error-баннер — прежний снимок остаётся на экране (placeholderData) */}
      {isError && (
        <div className="border-destructive/40 bg-destructive/5 rounded-md border p-4 text-sm">
          <p className="font-medium">{t("performance.errors.loadFailed")}</p>
          <Button
            variant="link"
            className="px-0"
            onClick={() => {
              void refetch().then((r) => {
                if (r.isSuccess) toast.success(t("common.refresh"));
              });
            }}
          >
            {t("common.refresh")}
          </Button>
        </div>
      )}

      {isLoading && !snapshot ? (
        <div className="space-y-6">
          <Skeleton className="h-12 w-full" />
          <Skeleton className="h-44 w-full" />
          <Skeleton className="h-72 w-full" />
        </div>
      ) : snapshot && derived ? (
        <>
          <VerdictBanner verdict={derived.verdict} />

          {snapshot.attributionIncomplete && (
            <AttributionWarningBanner processesInaccessible={snapshot.processesInaccessible} />
          )}

          <SaturationGauges
            snapshot={snapshot}
            onResourceClick={(r) => setLayer(layerForResource(r, derived.families))}
          />
        </>
      ) : null}

      {/* Drill-down воронки (MLC-207): переключатель слоёв Хост/1С/SQL с авто-фокусом по вердикту.
          Рендерится ВСЕГДА — 1С/SQL это независимые live-источники (MLC-067/069), не зависят от
          загрузки host-снимка; families/measuring берутся из derived (при отсутствии — []/false).
          forceMount внутри сохраняет фоновый polling неактивных слоёв. */}
      <PerformanceDrillDown
        layer={layer}
        onLayerChange={setLayer}
        families={derived?.families ?? []}
        measuring={measuring}
        paused={isPaused}
      />

      {/* Единый блок «Блокировки» (MLC-210): сводит цепочки блокировок SQL и заблокированные сеансы
          1С в одно место. Рендерится ВСЕГДА (как drill-down) — блокировки это сквозной сигнал
          контеншена, не зависит от host-снимка; читает оба live-источника (общий кэш React Query). */}
      <PerformanceBlockingBlock paused={isPaused} />

      {/* Запись по требованию (MLC-070/071) — единственный персистируемый источник: старт/стоп
          (Admin) + список расследований + просмотр (график host во времени + виновники за период). */}
      <RecordingSection />
    </div>
  );
}
