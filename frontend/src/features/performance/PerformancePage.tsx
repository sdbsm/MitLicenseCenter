import { useState } from "react";
import { useTranslation } from "react-i18next";
import { Tabs, TabsList, TabsTrigger } from "@/components/ui/tabs";
import { InvestigationMode } from "./InvestigationMode";
import { ObservationMode } from "./ObservationMode";

/** Режим просмотра раздела «Производительность» (ADR-57). */
type PerformanceMode = "observation" | "investigation";

/**
 * Контейнер раздела «Производительность» (MLC-241, ADR-57) — двухрежимная оболочка.
 *
 * Переключатель режимов в шапке:
 *   «Наблюдение»    — вся live-панель (ObservationMode): вердикт / светофор /
 *                     drill-down / блокировки / запись по требованию. Дефолт.
 *   «Расследование» — InvestigationMode: Мастер запуска (экран 2) или Прогресс
 *                     активного сбора (экран 6); Дело/Отчёт/Список — MLC-243/244.
 *
 * LiveControls и индикатор свежести живут внутри ObservationMode — в режиме
 * «Расследование» live-управление неуместно.
 */
export function PerformancePage() {
  const { t } = useTranslation();
  const [mode, setMode] = useState<PerformanceMode>("observation");

  return (
    <div className="space-y-6">
      {/* Шапка раздела: заголовок + переключатель режимов */}
      <div className="flex items-start justify-between gap-4">
        <div className="space-y-1">
          <h2 className="text-2xl font-semibold tracking-tight">{t("performance.title")}</h2>
          <p className="text-muted-foreground text-sm">{t("performance.subtitle")}</p>
        </div>

        {/* Переключатель «Наблюдение» | «Расследование» (ADR-57, спека §Общая структура) */}
        <Tabs value={mode} onValueChange={(v) => setMode(v as PerformanceMode)}>
          <TabsList>
            <TabsTrigger value="observation">{t("performance.modes.observation")}</TabsTrigger>
            <TabsTrigger value="investigation">{t("performance.modes.investigation")}</TabsTrigger>
          </TabsList>
        </Tabs>
      </div>

      {/* Контент режима */}
      {mode === "observation" ? (
        <ObservationMode onStartInvestigation={() => setMode("investigation")} />
      ) : (
        <InvestigationMode />
      )}
    </div>
  );
}
