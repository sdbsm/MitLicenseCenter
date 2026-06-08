import type { CpuMetrics, DiskMetrics, MemoryMetrics } from "./types";

/**
 * Светофор сатурации (методика ADR-26, уровень 1 «что насыщено»). Состояние
 * ресурса определяется НЕ голым процентом утилизации, а сигналами насыщения:
 * длина очереди процессора, латентность диска, страничный обмен. Чистые функции —
 * без React/WMI, целиком покрыты unit-тестами (паттерн `InfobaseValidationRules`).
 */
export type Saturation = "ok" | "warn" | "crit";

// «Хуже из двух» — общий ресурс настолько здоров, насколько здоров его худший
// сигнал (утилизация ИЛИ насыщение). Порядок строгий: crit > warn > ok.
function worst(a: Saturation, b: Saturation): Saturation {
  if (a === "crit" || b === "crit") return "crit";
  if (a === "warn" || b === "warn") return "warn";
  return "ok";
}

function band(value: number, warnAt: number, critAt: number): Saturation {
  if (value >= critAt) return "crit";
  if (value >= warnAt) return "warn";
  return "ok";
}

// CPU: утилизация (бэнды зеркалят семантику лицензий — 75/90%, см. 06_UI_DESIGN §3)
// УХудшается длиной очереди процессора — главный сигнал нехватки CPU. Абсолютные
// пороги очереди (а не на ядро): сервер 1С обычно многоядерный, устойчивая очередь
// ≥ 4 готовых-но-ждущих потоков = ощутимая деградация откликов.
export const CPU_PERCENT_WARN = 75;
export const CPU_PERCENT_CRIT = 90;
export const CPU_QUEUE_WARN = 2;
export const CPU_QUEUE_CRIT = 4;

export function cpuSaturation(cpu: CpuMetrics): Saturation {
  return worst(
    band(cpu.totalPercent, CPU_PERCENT_WARN, CPU_PERCENT_CRIT),
    band(cpu.queueLength, CPU_QUEUE_WARN, CPU_QUEUE_CRIT)
  );
}

// RAM: занятая доля ухудшается страничным обменом. Pages/sec — ранний признак
// нехватки физической памяти (система компенсирует диском); устойчивые сотни
// hard-фолтов в секунду = память под давлением ещё до того, как занятость близка к 100%.
export const RAM_PERCENT_WARN = 80;
export const RAM_PERCENT_CRIT = 90;
export const RAM_PAGES_WARN = 500;
export const RAM_PAGES_CRIT = 1000;

export function ramUsedPercent(memory: MemoryMetrics): number {
  if (memory.totalMBytes <= 0) return 0;
  const used = memory.totalMBytes - memory.availableMBytes;
  return Math.min(100, Math.max(0, (used / memory.totalMBytes) * 100));
}

export function ramSaturation(memory: MemoryMetrics): Saturation {
  return worst(
    band(ramUsedPercent(memory), RAM_PERCENT_WARN, RAM_PERCENT_CRIT),
    band(memory.pagesPerSec, RAM_PAGES_WARN, RAM_PAGES_CRIT)
  );
}

// Диск: латентность (секунды на операцию) — главный сигнал насыщения СХД, важнее
// IOPS. Пороги 10/20 мс — классический ориентир для дисков под SQL/1С (< 10 мс
// хорошо, > 20 мс — тормозит); берём ХУДШУЮ из чтения/записи. Длина очереди — вторичный.
export const DISK_LATENCY_WARN_SEC = 0.01; // 10 мс
export const DISK_LATENCY_CRIT_SEC = 0.02; // 20 мс
export const DISK_QUEUE_WARN = 2;
export const DISK_QUEUE_CRIT = 4;

export function diskMaxLatencySec(disk: DiskMetrics): number {
  return Math.max(disk.avgReadSecPerOp, disk.avgWriteSecPerOp);
}

export function diskSaturation(disk: DiskMetrics): Saturation {
  return worst(
    band(diskMaxLatencySec(disk), DISK_LATENCY_WARN_SEC, DISK_LATENCY_CRIT_SEC),
    band(disk.queueLength, DISK_QUEUE_WARN, DISK_QUEUE_CRIT)
  );
}

// Заполнение шкалы диска (0–100) — латентность не лежит на естественной 0–100
// оси, поэтому масштабируем худшую латентность к «крит-потолку» (× коэф.),
// читаемое значение в мс показывается отдельной подписью.
export const DISK_GAUGE_CEILING_SEC = DISK_LATENCY_CRIT_SEC * 2; // 40 мс = полная шкала

export function diskFillPercent(disk: DiskMetrics): number {
  return Math.min(100, (diskMaxLatencySec(disk) / DISK_GAUGE_CEILING_SEC) * 100);
}
