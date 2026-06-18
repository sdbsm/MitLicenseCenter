import { useState } from "react";
import { dominantFamily, type FamilyShare, type Verdict } from "./attribution";

/**
 * Drill-down раздела «Быстродействие» (MLC-207, Фаза 3 редизайна, Срез A): какой
 * слой воронки показан — хост (атрибуция семей), 1С или SQL. Авто-фокус наводит
 * на релевантный слой по вердикту (`culpritFamily`), но ручной выбор «прибивает»
 * слой — авто-фокус больше его не перебивает.
 */
export type DrillLayer = "host" | "onec" | "sql";

/**
 * Чистый маппинг ключа семьи → слой воронки. Семья 1С → слой «1С», MSSQL →
 * «SQL»; всё остальное (нет семьи, OsUpdate/Antivirus/Other, незнакомый ключ) →
 * слой «Хост». Общая основа для авто-фокуса по вердикту и клика по светофору
 * (Срез B). Без React — покрыто unit-тестом.
 */
export function familyToLayer(family: string | null | undefined): DrillLayer {
  switch (family) {
    case "OneC":
      return "onec";
    case "Mssql":
      return "sql";
    default:
      return "host";
  }
}

/**
 * Чистый маппинг вердикта → слой авто-фокуса. Виновник 1С → слой «1С», MSSQL →
 * «SQL»; всё остальное (нет виновника, OsUpdate/Antivirus/Other, узкое место —
 * диск, level ok/measuring) → слой «Хост». Без React — покрыто unit-тестом.
 */
export function autoFocusLayer(verdict: Verdict | null | undefined): DrillLayer {
  return familyToLayer(verdict?.culpritFamily);
}

/**
 * Слой воронки для клика по гейджу светофора (Срез B). CPU/RAM наводят на слой
 * доминирующей по этому ресурсу семьи (1С → «1С», MSSQL → «SQL», иначе «Хост»).
 *
 * Диск всегда → слой «SQL»: у диска нет атрибуции по семьям процессов (счётчики
 * латентности/очереди — хостовые, не разложимы по процессам), а единственное
 * предметное доказательство дисковой нагрузки — IO-stall по базам — живёт в
 * SQL-слое (`SqlDatabaseIoTable`). Поэтому клик по диску ведёт туда, где есть что
 * расследовать.
 */
export function layerForResource(
  resource: "cpu" | "ram" | "disk",
  families: FamilyShare[]
): DrillLayer {
  switch (resource) {
    case "cpu":
      return familyToLayer(dominantFamily(families, "cpu")?.family);
    case "ram":
      return familyToLayer(dominantFamily(families, "ram")?.family);
    case "disk":
      return "sql";
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
