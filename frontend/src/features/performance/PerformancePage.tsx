import { useTranslation } from "react-i18next";
import { toast } from "sonner";
import { LiveControls } from "@/components/LiveControls";
import { RelativeTime } from "@/components/ui/RelativeTime";
import { Button } from "@/components/ui/button";
import { Skeleton } from "@/components/ui/skeleton";
import { AttributionWarningBanner } from "./AttributionWarningBanner";
import { PerformanceDrillDown } from "./PerformanceDrillDown";
import { RecordingSection } from "./RecordingSection";
import { SaturationGauges } from "./SaturationGauges";
import { VerdictBanner } from "./VerdictBanner";
import { layerForResource, useDrillDownFocus } from "./useDrillDownFocus";
import { usePerformancePage } from "./usePerformancePage";

/**
 * Раздел «Быстродействие» — стартовый live-экран (MLC-065, ADR-26). Снимок host
 * «сейчас»: вердикт «почему тормозит» + светофор ресурсов (гейджи) + атрибуция по
 * семьям процессов. Pull-по-требованию (polling 5с), ничего не персистится.
 * Контейнер по паттерну `SessionsPage`: header со свежестью + error-баннер + секции.
 */
export function PerformancePage() {
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
      {/* Header + индикатор свежести (06_UI_DESIGN §8) */}
      <div className="flex items-start justify-between gap-4">
        <div className="space-y-1">
          <h2 className="text-2xl font-semibold tracking-tight">{t("performance.title")}</h2>
          <p className="text-muted-foreground text-sm">{t("performance.subtitle")}</p>
        </div>
        <div className="flex items-center gap-3">
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

      {/* Запись по требованию (MLC-070/071) — единственный персистируемый источник: старт/стоп
          (Admin) + список расследований + просмотр (график host во времени + виновники за период). */}
      <RecordingSection />
    </div>
  );
}
