import { useTranslation } from "react-i18next";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Tabs, TabsContent, TabsList, TabsTrigger } from "@/components/ui/tabs";
import { OneCLoadSection } from "./OneCLoadSection";
import { ProcessFamilyAttribution } from "./ProcessFamilyAttribution";
import { SqlLoadSection } from "./SqlLoadSection";
import type { FamilyShare } from "./attribution";
import type { DrillLayer } from "./useDrillDownFocus";

interface PerformanceDrillDownProps {
  layer: DrillLayer;
  onLayerChange: (l: DrillLayer) => void;
  families: FamilyShare[];
  measuring: boolean;
  paused: boolean;
}

/**
 * Drill-down раздела «Быстродействие» (MLC-207, Срез A): сегментированный
 * переключатель трёх слоёв воронки — Хост (атрибуция семей) / 1С / SQL — с
 * авто-фокусом по вердикту (состояние в `useDrillDownFocus`). Контролируемые
 * `Tabs`. Все три `TabsContent` — `forceMount`: неактивные слои остаются
 * смонтированными (Radix скрывает их `hidden`), их live-источники (1С/SQL)
 * продолжают фоновый polling, переключение мгновенно без повторного «измеряю…».
 */
export function PerformanceDrillDown({
  layer,
  onLayerChange,
  families,
  measuring,
  paused,
}: PerformanceDrillDownProps) {
  const { t } = useTranslation();

  return (
    <Tabs value={layer} onValueChange={(v) => onLayerChange(v as DrillLayer)}>
      <TabsList>
        <TabsTrigger value="host">{t("performance.drilldown.layers.host")}</TabsTrigger>
        <TabsTrigger value="onec">{t("performance.drilldown.layers.onec")}</TabsTrigger>
        <TabsTrigger value="sql">{t("performance.drilldown.layers.sql")}</TabsTrigger>
      </TabsList>

      <TabsContent value="host" forceMount hidden={layer !== "host"}>
        <Card>
          <CardHeader>
            <CardTitle>{t("performance.attribution.title")}</CardTitle>
            <p className="text-muted-foreground text-sm">{t("performance.attribution.subtitle")}</p>
          </CardHeader>
          <CardContent>
            <ProcessFamilyAttribution families={families} measuring={measuring} />
          </CardContent>
        </Card>
      </TabsContent>

      <TabsContent value="onec" forceMount hidden={layer !== "onec"}>
        <OneCLoadSection paused={paused} />
      </TabsContent>

      <TabsContent value="sql" forceMount hidden={layer !== "sql"}>
        <SqlLoadSection paused={paused} />
      </TabsContent>
    </Tabs>
  );
}
