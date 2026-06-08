import { useTranslation } from "react-i18next";
import { toast } from "sonner";
import { RelativeTime } from "@/components/ui/RelativeTime";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Skeleton } from "@/components/ui/skeleton";
import { AttributionWarningBanner } from "./AttributionWarningBanner";
import { OneCLoadSection } from "./OneCLoadSection";
import { ProcessFamilyAttribution } from "./ProcessFamilyAttribution";
import { SaturationGauges } from "./SaturationGauges";
import { SqlLoadSection } from "./SqlLoadSection";
import { VerdictBanner } from "./VerdictBanner";
import { usePerformancePage } from "./usePerformancePage";

/**
 * Раздел «Быстродействие» — стартовый live-экран (MLC-065, ADR-26). Снимок host
 * «сейчас»: вердикт «почему тормозит» + светофор ресурсов (гейджи) + атрибуция по
 * семьям процессов. Pull-по-требованию (polling 5с), ничего не персистится.
 * Контейнер по паттерну `SessionsPage`: header со свежестью + error-баннер + секции.
 */
export function PerformancePage() {
  const { t } = useTranslation();
  const { snapshot, measuring, derived, isLoading, isError, refetch, failureCount } =
    usePerformancePage();

  return (
    <div className="space-y-6">
      {/* Header + индикатор свежести (06_UI_DESIGN §8) */}
      <div className="flex items-start justify-between gap-4">
        <div className="space-y-1">
          <h2 className="text-2xl font-semibold tracking-tight">{t("performance.title")}</h2>
          <p className="text-muted-foreground text-sm">{t("performance.subtitle")}</p>
        </div>
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

          <SaturationGauges snapshot={snapshot} />

          <Card>
            <CardHeader>
              <CardTitle>{t("performance.attribution.title")}</CardTitle>
              <p className="text-muted-foreground text-sm">
                {t("performance.attribution.subtitle")}
              </p>
            </CardHeader>
            <CardContent>
              <ProcessFamilyAttribution families={derived.families} measuring={measuring} />
            </CardContent>
          </Card>
        </>
      ) : null}

      {/* Drill-down «кто грузит внутри 1С» — собственный live-источник (MLC-067), не зависит
          от загрузки host-снимка: рендерится всегда, управляет своим состоянием сам. */}
      <OneCLoadSection />

      {/* Drill-down «1С грузит SQL?» — собственный live-источник (MLC-069), своя Zod-граница
          и degraded по статусу DMV-пробы; рендерится всегда, независимо от host/1С-снимков. */}
      <SqlLoadSection />
    </div>
  );
}
