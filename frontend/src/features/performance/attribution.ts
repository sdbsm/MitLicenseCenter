import type { HostMetricsSnapshot, ProcessGroupUsage } from "./types";
import { cpuSaturation, diskSaturation, ramSaturation, type Saturation } from "./thresholds";

/**
 * Уровень 2 методики ADR-26 «кто потребляет»: атрибуция насыщенного ресурса по
 * семьям процессов и сводный вердикт «почему тормозит». Чистые функции (без
 * React) — покрыты unit-тестами.
 */

// Известные семьи в порядке отображения (стек/таблица). Незнакомый ключ из
// настраиваемого маппинга бэкенда деградирует к «Прочее» (подпись), но не теряется.
export const KNOWN_FAMILIES = ["OneC", "Mssql", "OsUpdate", "Antivirus", "Other"] as const;

// Доля семьи в насыщенном ресурсе. `cpuPercent` суммарный по семье; `ramBytes` —
// сумма рабочих наборов. `share*` — доля от общего по всем семьям (0–1) для стека.
export interface FamilyShare {
  family: string;
  cpuPercent: number;
  ramBytes: number;
  processCount: number;
  cpuShare: number;
  ramShare: number;
}

function familyOrder(family: string): number {
  const idx = KNOWN_FAMILIES.indexOf(family as (typeof KNOWN_FAMILIES)[number]);
  // Неизвестные ключи — в конец (после «Other»), сохраняя стабильный порядок.
  return idx === -1 ? KNOWN_FAMILIES.length : idx;
}

export function toFamilyShares(groups: readonly ProcessGroupUsage[]): FamilyShare[] {
  const totalCpu = groups.reduce((s, g) => s + Math.max(0, g.cpuPercent), 0);
  const totalRam = groups.reduce((s, g) => s + Math.max(0, g.ramBytes), 0);
  return groups
    .map((g) => ({
      family: g.family,
      cpuPercent: g.cpuPercent,
      ramBytes: g.ramBytes,
      processCount: g.processCount,
      cpuShare: totalCpu > 0 ? Math.max(0, g.cpuPercent) / totalCpu : 0,
      ramShare: totalRam > 0 ? Math.max(0, g.ramBytes) / totalRam : 0,
    }))
    .sort((a, b) => familyOrder(a.family) - familyOrder(b.family));
}

// Семья — главный потребитель ресурса (по доле). Используется в вердикте.
export function dominantFamily(
  shares: readonly FamilyShare[],
  by: "cpu" | "ram"
): FamilyShare | null {
  let top: FamilyShare | null = null;
  for (const s of shares) {
    const v = by === "cpu" ? s.cpuShare : s.ramShare;
    const tv = top ? (by === "cpu" ? top.cpuShare : top.ramShare) : -1;
    if (v > tv) top = s;
  }
  return top && (by === "cpu" ? top.cpuShare : top.ramShare) > 0 ? top : null;
}

// Сводный вердикт раздела: худшее состояние трёх ресурсов + насыщенный ресурс +
// главный потребитель по нему. Это прямой ответ «почему 1С тормозит». На первом
// poll'е (measuring) дельты не готовы — вердикт не выносится (level=measuring).
export type VerdictLevel = "ok" | "warn" | "crit" | "measuring";

export interface Verdict {
  level: VerdictLevel;
  // Ресурс-узкое-место (для warn/crit): "cpu" | "ram" | "disk" | null.
  resource: "cpu" | "ram" | "disk" | null;
  // Главный потребитель узкого ресурса (ключ семьи) — для CPU/RAM; для диска null.
  culpritFamily: string | null;
}

const RANK: Record<Saturation, number> = { ok: 0, warn: 1, crit: 2 };

export function computeVerdict(snapshot: HostMetricsSnapshot): Verdict {
  if (snapshot.measuring) {
    return { level: "measuring", resource: null, culpritFamily: null };
  }

  const states = {
    cpu: cpuSaturation(snapshot.cpu),
    ram: ramSaturation(snapshot.memory),
    disk: diskSaturation(snapshot.disk),
  } as const;

  // Узкое место — ресурс с худшим состоянием (приоритет cpu→ram→disk при равенстве).
  const order: Array<keyof typeof states> = ["cpu", "ram", "disk"];
  const resource = order.reduce(
    (acc, r) => (RANK[states[r]] > RANK[states[acc]] ? r : acc),
    order[0]
  );
  const level = states[resource];

  if (level === "ok") {
    return { level: "ok", resource: null, culpritFamily: null };
  }

  const shares = toFamilyShares(snapshot.processGroups);
  const culprit =
    resource === "disk" ? null : dominantFamily(shares, resource === "cpu" ? "cpu" : "ram");

  return { level, resource, culpritFamily: culprit?.family ?? null };
}
