import type { StatusBadgeVariant } from "@/components/ui/StatusBadge";
import type { OneCSessionLoad } from "./types";

/**
 * Чистая логика экрана «нагрузка по сеансам/процессам» 1С (MLC-067): классификация
 * состояния сеанса, ранжирование «кто грузит» и форматирование perf-значений с
 * честным «—» для отсутствующих (`null`) полей. Без React — покрыто unit-тестами.
 */

// Текущий вызов дольше этого порога подсвечивается как «долгий/зависший». На разведке
// MLC-063 нормальные вызовы под нагрузкой были 0.3–1.5 с → 5 с с запасом выделяет аномалию.
export const SESSION_LONG_CALL_MS = 5_000;
// Сеанс без активного вызова, молчащий дольше этого — «молчит» (кандидат на зависший клиент).
export const SESSION_SILENT_SEC = 300;

// Производительность процесса (APDEX-подобная, `capacity` 1000): чем ниже, тем хуже.
export const AVAILABLE_PERF_WARN = 800;
export const AVAILABLE_PERF_CRIT = 500;

// Состояние сеанса — приоритет сверху вниз: блокировка важнее долгого вызова и т.д.
// `blocked` — заблокирован СУБД/управляемой блокировкой (контеншен); `long` — текущий
// вызов аномально долгий; `active` — есть активный вызов; `silent` — давно молчит без
// вызова; `idle` — простаивает. blocked/long — главные «виновники тормозов».
export type SessionState = "blocked" | "long" | "active" | "silent" | "idle";

export function classifySession(s: OneCSessionLoad, capturedAtUtc: string): SessionState {
  if ((s.blockedByDbms ?? 0) !== 0 || (s.blockedByLs ?? 0) !== 0) return "blocked";
  const duration = s.durationCurrent ?? 0;
  if (duration >= SESSION_LONG_CALL_MS) return "long";
  if (duration > 0) return "active";
  if (s.lastActiveAtUtc) {
    const idleSec = (Date.parse(capturedAtUtc) - Date.parse(s.lastActiveAtUtc)) / 1_000;
    if (Number.isFinite(idleSec) && idleSec >= SESSION_SILENT_SEC) return "silent";
  }
  return "idle";
}

// Заблокированные сеансы 1С (контеншен): ждут освобождения СУБД-блокировки (`blockedByDbms`)
// или управляемой блокировки (`blockedByLs`). Стабильная сортировка по номеру сеанса (null тонут)
// — детерминированный порядок для единого блока «Блокировки» (MLC-210). Не мутирует вход.
export function blockedSessions(sessions: readonly OneCSessionLoad[]): OneCSessionLoad[] {
  return sessions
    .filter((s) => (s.blockedByDbms ?? 0) !== 0 || (s.blockedByLs ?? 0) !== 0)
    .sort((a, b) => (a.sessionNumber ?? Infinity) - (b.sessionNumber ?? Infinity));
}

export const SESSION_STATE_VARIANT: Record<SessionState, StatusBadgeVariant> = {
  blocked: "danger",
  long: "warning",
  active: "info",
  silent: "neutral",
  idle: "neutral",
};

// Ранг «кто грузит» для сортировки: по текущему ЦП-времени, при равенстве — по длительности
// текущего вызова. Отсутствующие (`null`) метрики тонут вниз (трактуются как наименьшие).
export function sortSessionsByLoad(sessions: readonly OneCSessionLoad[]): OneCSessionLoad[] {
  return [...sessions].sort((a, b) => {
    const cpu = (b.cpuTimeCurrent ?? -1) - (a.cpuTimeCurrent ?? -1);
    if (cpu !== 0) return cpu;
    return (b.durationCurrent ?? -1) - (a.durationCurrent ?? -1);
  });
}

// Полоса производительности процесса для тинта (ниже — хуже). null → нет данных.
export function availablePerformanceBand(v: number | null): "ok" | "warn" | "crit" | null {
  if (v === null) return null;
  if (v >= AVAILABLE_PERF_WARN) return "ok";
  if (v >= AVAILABLE_PERF_CRIT) return "warn";
  return "crit";
}

// Длительность в мс → «—» при null, «N мс» до секунды, иначе «N.N с».
export function formatMs(ms: number | null): string {
  if (ms === null) return "—";
  if (ms < 1_000) return `${Math.round(ms)} мс`;
  return `${(ms / 1_000).toFixed(1)} с`;
}

// Память в байтах → «—» при null, иначе знаковые МБ (rac отдаёт отрицательную текущую
// память в момент GC — знак сохраняем, это не ошибка).
export function formatSignedMb(bytes: number | null): string {
  if (bytes === null) return "—";
  const mb = bytes / 1024 ** 2;
  const sign = mb < 0 ? "−" : "";
  return `${sign}${Math.abs(mb).toFixed(1)} МБ`;
}

// Размер памяти процесса (всегда ≥ 0) → «—»/МБ/ГБ.
export function formatBytes(bytes: number | null): string {
  if (bytes === null) return "—";
  const gb = bytes / 1024 ** 3;
  if (gb >= 1) return `${gb.toFixed(1)} ГБ`;
  return `${(bytes / 1024 ** 2).toFixed(0)} МБ`;
}

// Средняя длительность вызова (rac отдаёт в секундах, дробную) → «—»/мс.
export function formatAvgCallMs(seconds: number | null): string {
  if (seconds === null) return "—";
  return `${Math.round(seconds * 1_000)} мс`;
}

// Короткий вид UUID (первые 8 символов без дефисов) — для плотной таблицы; полный — в тултипе.
export function shortUuid(uuid: string | null): string {
  if (!uuid) return "—";
  return uuid.replace(/-/g, "").slice(0, 8);
}
