import { useState } from "react";
import type { Verdict } from "./attribution";

/**
 * Drill-down раздела «Быстродействие» (MLC-207, Фаза 3 редизайна, Срез A): какой
 * слой воронки показан — хост (атрибуция семей), 1С или SQL. Авто-фокус наводит
 * на релевантный слой по вердикту (`culpritFamily`), но ручной выбор «прибивает»
 * слой — авто-фокус больше его не перебивает.
 */
export type DrillLayer = "host" | "onec" | "sql";

/**
 * Чистый маппинг вердикта → слой авто-фокуса. Виновник 1С → слой «1С», MSSQL →
 * «SQL»; всё остальное (нет виновника, OsUpdate/Antivirus/Other, узкое место —
 * диск, level ok/measuring) → слой «Хост». Без React — покрыто unit-тестом.
 */
export function autoFocusLayer(verdict: Verdict | null | undefined): DrillLayer {
  switch (verdict?.culpritFamily) {
    case "OneC":
      return "onec";
    case "Mssql":
      return "sql";
    default:
      return "host";
  }
}

/**
 * Авто-фокус с ручным override. `layer` стартует с подсказки вердикта и
 * авто-следует за её сменой, пока пользователь не выберет слой руками — после
 * чего `pinned` фиксирует выбор и авто-фокус больше не вмешивается. Состояние
 * живёт здесь, чтобы Срез B (клик по светофору) мог им управлять.
 */
export function useDrillDownFocus(verdict: Verdict | null | undefined): {
  layer: DrillLayer;
  setLayer: (l: DrillLayer) => void;
} {
  const suggested = autoFocusLayer(verdict);
  const [layer, setLayerState] = useState<DrillLayer>(suggested);
  const [pinned, setPinned] = useState(false);
  // Предыдущая подсказка хранится в state (а не ref) — чтение/запись ref во время
  // рендера запрещены React Compiler; setState во время рендера допустим.
  const [prevSuggested, setPrevSuggested] = useState<DrillLayer>(suggested);

  // Авто-следование за вердиктом — корректировка состояния во время рендера
  // (паттерн React «adjust state on prop change»), без эффекта и каскадных
  // ре-рендеров. Только пока слой не прибит вручную; это обновление НЕ пинит.
  if (suggested !== prevSuggested) {
    setPrevSuggested(suggested);
    if (!pinned) setLayerState(suggested);
  }

  const setLayer = (l: DrillLayer) => {
    setLayerState(l);
    setPinned(true);
  };

  return { layer, setLayer };
}
