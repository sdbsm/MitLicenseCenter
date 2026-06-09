import type { StatusBadgeVariant } from "@/components/ui/StatusBadge";
import type {
  OneCProcessLoad,
  OneCSessionLoad,
  PerfRecordingStatus,
  RecordingSample,
  SqlActiveRequest,
} from "./types";

/**
 * Чистая логика просмотра записи быстродействия (MLC-071): свёртка ряда сэмплов в график
 * host-метрик во времени и «топ-виновников за период» для 1С/SQL. Без React — покрыто
 * unit-тестами. Форматтеры значений переиспользуются из `onecLoad`/`sqlLoad` (те же формы).
 *
 * «За период» = пиковый срез по каждому виновнику: для сеанса/процесса/запроса берём тот
 * сэмпл, где он грузил сильнее всего (ЦП-время), и его агрегаты показываем в live-таблицах
 * 067/069. Это честнее усреднения — расследование ищет момент, когда «припекло».
 */

// ── Бейдж статуса записи ────────────────────────────────────────────────────────────────────
export const RECORDING_STATUS_VARIANT: Record<PerfRecordingStatus, StatusBadgeVariant> = {
  Active: "info",
  Stopped: "neutral",
  Interrupted: "warning",
};

// ── График host-метрик во времени ───────────────────────────────────────────────────────────
// Один ряд = один сэмпл. CPU% и «память занято %» — на левой оси 0–100 (сопоставимы); латентность
// диска (худшая из чтения/записи, переведённая в мс) — на правой оси (иные единицы). Сэмплы с
// `measuring=true` (первый тик записи) дают дельта-нули — помечаем, чтобы график не врал «0».
export interface HostChartRow {
  sampleUtc: string;
  cpuPercent: number;
  memoryUsedPercent: number;
  diskLatencyMs: number;
  measuring: boolean;
}

export function toHostChartRows(samples: readonly RecordingSample[]): HostChartRow[] {
  return samples.map((s) => {
    const used =
      s.memoryTotalMBytes > 0
        ? ((s.memoryTotalMBytes - s.memoryAvailableMBytes) / s.memoryTotalMBytes) * 100
        : 0;
    const latencySec = Math.max(s.diskAvgReadSecPerOp, s.diskAvgWriteSecPerOp);
    return {
      sampleUtc: s.sampleUtc,
      cpuPercent: Math.round(s.cpuPercent * 10) / 10,
      memoryUsedPercent: Math.round(Math.max(0, Math.min(100, used)) * 10) / 10,
      diskLatencyMs: Math.round(latencySec * 1000 * 10) / 10,
      measuring: s.measuring,
    };
  });
}

// ── Топ-виновники 1С за период ──────────────────────────────────────────────────────────────
// Сеанс мог появляться во многих сэмплах — берём его пиковый срез (max cpu-time-current). Так
// в таблицу попадает по одной строке на сеанс, отражающей момент его наибольшей нагрузки.
export function aggregateOneCSessions(samples: readonly RecordingSample[]): OneCSessionLoad[] {
  const peak = new Map<string, OneCSessionLoad>();
  for (const s of samples) {
    for (const session of s.oneC?.sessions ?? []) {
      const prev = peak.get(session.sessionId);
      if (!prev || (session.cpuTimeCurrent ?? -1) > (prev.cpuTimeCurrent ?? -1)) {
        peak.set(session.sessionId, session);
      }
    }
  }
  return [...peak.values()];
}

// Рабочий процесс за период — срез, где он сильнее всего «просел» (min available-perfomance;
// null трактуется как «нет данных» и не вытесняет реальное значение).
export function aggregateOneCProcesses(samples: readonly RecordingSample[]): OneCProcessLoad[] {
  const worst = new Map<string, OneCProcessLoad>();
  for (const s of samples) {
    for (const proc of s.oneC?.processes ?? []) {
      const prev = worst.get(proc.process);
      if (!prev || lowerPerf(proc.availablePerformance, prev.availablePerformance)) {
        worst.set(proc.process, proc);
      }
    }
  }
  return [...worst.values()];
}

// `a` хуже `b`, если у `a` есть значение и оно ниже (или у `b` значения нет).
function lowerPerf(a: number | null, b: number | null): boolean {
  if (a === null) return false;
  if (b === null) return true;
  return a < b;
}

// Последний по времени сэмпл, у которого есть 1С-данные — даёт `capturedAtUtc` для классификации
// состояния сеанса (молчит/завис) в таблице. null, если 1С ни в одном сэмпле не собрана.
export function lastOneCCapturedAt(samples: readonly RecordingSample[]): string | null {
  for (let i = samples.length - 1; i >= 0; i--) {
    if (samples[i].oneC) return samples[i].oneC!.capturedAtUtc;
  }
  return null;
}

export function hasOneCData(samples: readonly RecordingSample[]): boolean {
  return samples.some((s) => s.oneC && (s.oneC.sessions.length > 0 || s.oneC.processes.length > 0));
}

// ── Топ-виновники SQL за период ─────────────────────────────────────────────────────────────
// Активный запрос за период — пиковый срез сеанса по ЦП-времени (как у 1С-сеансов). Запись
// хранит только snapshot DMV-пробы (без атрибуции база→клиент — она добавляется лишь в live
// эндпоинте), поэтому таблицы получают пустую карту атрибуции и показывают клиент «—».
export function aggregateSqlRequests(samples: readonly RecordingSample[]): SqlActiveRequest[] {
  const peak = new Map<number, SqlActiveRequest>();
  for (const s of samples) {
    for (const req of s.sql?.activeRequests ?? []) {
      const prev = peak.get(req.sessionId);
      if (!prev || (req.cpuTimeMs ?? -1) > (prev.cpuTimeMs ?? -1)) {
        peak.set(req.sessionId, req);
      }
    }
  }
  return [...peak.values()];
}

export function hasSqlData(samples: readonly RecordingSample[]): boolean {
  return samples.some((s) => s.sql && s.sql.activeRequests.length > 0);
}
