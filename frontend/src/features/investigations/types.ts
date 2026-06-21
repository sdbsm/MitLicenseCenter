import { z } from "zod";
import { omittable } from "@/lib/apiSchema";

/**
 * Контракт-слой «Расследование производительности» (трек 1.2, MLC-239/243, этапы C/D). Зеркалит DTO бэкенда
 * (InvestigationContracts.cs + *AnalysisResult.cs) — Zod-схемы критичной границы (ADR-10.1 / MLC-016) + типы
 * через `z.infer`. Enum'ы приходят строкой (`JsonStringEnumConverter`); строки FE ТОЧНО совпадают с именами
 * членов Domain-enum'ов (frozen-int на проводе уходит именем). Экраны раздела — этап D (MLC-243); контракт +
 * хуки + i18n-enum.
 *
 * `null`-поля бэкенд ОПУСКАЕТ (`JsonIgnoreCondition.WhenWritingNull`): `stoppedAtUtc`/`stopReason`/
 * `tenantId`/`infobaseId` у активного/непривязанного дела не приходят как `null`, а отсутствуют —
 * объявлены через `omittable()` (урок [[api-omits-null-fields]]: `.nullable()` упал бы на отсутствующем
 * ключе). Parity-тесты покрывают omit-null.
 *
 * `finding.result` типизирован пер-Kind (MLC-243): LockAnalysisResult / SlowQueryAnalysisResult /
 * ExceptionAnalysisResult / DbmsLockAnalysisResult — parity с BE-DTO анализаторов (camelCase на
 * проводе, WhenWritingNull). Схемы НЕ используют .strict() — BE может добавить поля; лишние ключи
 * Zod игнорирует (толерантность).
 */

// Жизненный цикл дела (InvestigationStatus). Имена 1:1 с Domain-enum (frozen-int на проводе — именем).
export const investigationStatusSchema = z.enum([
  "Collecting",
  "Analyzing",
  "Completed",
  "Interrupted",
  "Failed",
]);

// Сценарий целевого сбора (InvestigationScenario). Имена 1:1 с Domain-enum.
export const investigationScenarioSchema = z.enum([
  "Locks",
  "SlowQueries",
  "Exceptions",
  "GeneralSlow",
  "DbmsLocks",
]);

// Причина остановки (InvestigationStopReason). Заполнена только у Completed; иначе поле опущено.
export const investigationStopReasonSchema = z.enum(["Manual", "TimeLimit", "DiskLimit", "Error"]);

// Вид находки (FindingKind) — какой анализатор дал результат. MLC-249: добавлен "Call" (frozen-int 4).
export const findingKindSchema = z.enum([
  "ManagedLocks",
  "SlowQueries",
  "Exceptions",
  "DbmsLocks",
  "Call",
]);

// ─── Пер-Kind схемы result (MLC-243, parity с BE-DTO анализаторов) ─────────────────────────────
// Поля — camelCase (JSON из C# record/class). Nullable-поля ОПУСКАЮТСЯ (WhenWritingNull) → omittable().
// Обязательные числа (DurationMicroseconds, DurationSeconds, Count и т.п.) → z.number().
// Не используем .strict(): BE может добавить поля; Zod по умолчанию игнорирует лишние ключи.

/** Ребро ожидания управляемой блокировки 1С (LockWaitEdge, MLC-233). */
const lockWaitEdgeSchema = z.object({
  ts: omittable(z.string()),
  waitingSessionId: omittable(z.string()),
  waitingUser: omittable(z.string()),
  waitingAppId: omittable(z.string()),
  blockingConnections: omittable(z.string()),
  regions: omittable(z.string()),
  lockMode: omittable(z.string()),
  waitDurationSeconds: omittable(z.number()),
  infobaseName: omittable(z.string()),
  rawProcessName: omittable(z.string()),
  context: omittable(z.string()),
  database: omittable(z.string()),
});

/** Таймаут ожидания управляемой блокировки 1С (LockTimeoutEntry, MLC-233). */
const lockTimeoutEntrySchema = z.object({
  ts: omittable(z.string()),
  sessionId: omittable(z.string()),
  user: omittable(z.string()),
  regions: omittable(z.string()),
  lockMode: omittable(z.string()),
  waitDurationSeconds: omittable(z.number()),
  infobaseName: omittable(z.string()),
  rawProcessName: omittable(z.string()),
  context: omittable(z.string()),
  waitConnections: omittable(z.string()),
});

/** Взаимоблокировка управляемых блокировок 1С (LockDeadlockEntry, MLC-233). */
const lockDeadlockEntrySchema = z.object({
  ts: omittable(z.string()),
  sessionId: omittable(z.string()),
  user: omittable(z.string()),
  regions: omittable(z.string()),
  lockMode: omittable(z.string()),
  durationSeconds: omittable(z.number()),
  infobaseName: omittable(z.string()),
  rawProcessName: omittable(z.string()),
  context: omittable(z.string()),
  waitConnections: omittable(z.string()),
});

/** LockAnalysisResult — результат анализа управляемых блокировок 1С (kind=ManagedLocks). */
export const lockAnalysisResultSchema = z.object({
  waitEdges: z.array(lockWaitEdgeSchema),
  timeouts: z.array(lockTimeoutEntrySchema),
  deadlocks: z.array(lockDeadlockEntrySchema),
  tlockEventsProcessed: z.number(),
  skippedEvents: z.number(),
});

/** Запись об одном медленном запросе к СУБД (SlowQueryEntry, MLC-234). */
const slowQueryEntrySchema = z.object({
  ts: omittable(z.string()),
  durationMicroseconds: z.number(),
  durationSeconds: z.number(),
  sql: omittable(z.string()),
  context: omittable(z.string()),
  dbPid: omittable(z.string()),
  rows: omittable(z.string()),
  rowsAffected: omittable(z.string()),
  database: omittable(z.string()),
  infobaseName: omittable(z.string()),
  rawProcessName: omittable(z.string()),
  sessionId: omittable(z.string()),
  user: omittable(z.string()),
  planText: omittable(z.string()),
});

/** Группа похожих SQL-запросов (SlowQueryGroup, MLC-234). */
const slowQueryGroupSchema = z.object({
  normalizedSql: z.string(),
  count: z.number(),
  totalDurationMicroseconds: z.number(),
  maxDurationMicroseconds: z.number(),
  totalDurationSeconds: z.number(),
  maxDurationSeconds: z.number(),
});

/** SlowQueryAnalysisResult — результат анализа долгих запросов (kind=SlowQueries). */
export const slowQueryAnalysisResultSchema = z.object({
  topQueries: z.array(slowQueryEntrySchema),
  similarGroups: z.array(slowQueryGroupSchema),
  totalDbmssqlEvents: z.number(),
  eventsAboveThreshold: z.number(),
  skippedEvents: z.number(),
});

/** Группа однотипных исключений 1С (ExceptionGroup, MLC-235). */
const exceptionGroupSchema = z.object({
  exceptionType: omittable(z.string()),
  normalizedDescr: z.string(),
  sampleDescr: omittable(z.string()),
  sampleContext: omittable(z.string()),
  count: z.number(),
  isDatabaseException: z.boolean(),
  infobaseName: omittable(z.string()),
  rawProcessName: omittable(z.string()),
  firstTs: omittable(z.string()),
  lastTs: omittable(z.string()),
});

/** ExceptionAnalysisResult — результат анализа исключений платформы (kind=Exceptions). */
export const exceptionAnalysisResultSchema = z.object({
  topExceptions: z.array(exceptionGroupSchema),
  totalExcpEvents: z.number(),
  databaseExceptionEvents: z.number(),
  skippedEvents: z.number(),
});

/** Ребро СУБД-блокировки: жертва → источник (DbmsLockWaitEdge, MLC-236). */
const dbmsLockWaitEdgeSchema = z.object({
  victimTs: omittable(z.string()),
  victimConnectId: omittable(z.string()),
  victimLksrc: omittable(z.string()),
  victimLkpto: omittable(z.string()),
  victimSql: omittable(z.string()),
  victimContext: omittable(z.string()),
  victimLkpid: omittable(z.string()),
  sourceConnectId: omittable(z.string()),
  sourceLkato: omittable(z.string()),
  sourceLkaid: omittable(z.string()),
  sourceSql: omittable(z.string()),
  sourceContext: omittable(z.string()),
  infobaseName: omittable(z.string()),
  rawProcessName: omittable(z.string()),
  database: omittable(z.string()),
  sourceMatched: z.boolean(),
});

/** DbmsLockAnalysisResult — результат анализа СУБД-блокировок (kind=DbmsLocks). */
export const dbmsLockAnalysisResultSchema = z.object({
  waitEdges: z.array(dbmsLockWaitEdgeSchema),
  lkEventsProcessed: z.number(),
  unmatchedVictimCount: z.number(),
  skippedEvents: z.number(),
});

/** Запись об одном серверном вызове 1С (CallEntry, MLC-249). ⚠ У CALL нет p:processName → нет infobaseName. */
const callEntrySchema = z.object({
  ts: omittable(z.string()),
  durationMicroseconds: z.number(),
  durationSeconds: z.number(),
  context: omittable(z.string()),
  method: omittable(z.string()),
  cpuTime: omittable(z.string()),
  memory: omittable(z.string()),
});

/** Группа серверных вызовов 1С по контексту (CallGroup, MLC-249). */
const callGroupSchema = z.object({
  context: z.string(),
  count: z.number(),
  totalDurationMicroseconds: z.number(),
  maxDurationMicroseconds: z.number(),
  totalDurationSeconds: z.number(),
  maxDurationSeconds: z.number(),
});

/** CallAnalysisResult — результат анализа серверных вызовов 1С (kind=Call). */
export const callAnalysisResultSchema = z.object({
  topCalls: z.array(callEntrySchema),
  similarGroups: z.array(callGroupSchema),
  totalCallEvents: z.number(),
  eventsAboveThreshold: z.number(),
  skippedEvents: z.number(),
});

// Экспортируемые типы для sub-схем (используются в компонентах)
export type LockWaitEdge = z.infer<typeof lockWaitEdgeSchema>;
export type LockTimeoutEntry = z.infer<typeof lockTimeoutEntrySchema>;
export type LockDeadlockEntry = z.infer<typeof lockDeadlockEntrySchema>;
export type LockAnalysisResult = z.infer<typeof lockAnalysisResultSchema>;
export type SlowQueryEntry = z.infer<typeof slowQueryEntrySchema>;
export type SlowQueryGroup = z.infer<typeof slowQueryGroupSchema>;
export type SlowQueryAnalysisResult = z.infer<typeof slowQueryAnalysisResultSchema>;
export type ExceptionGroup = z.infer<typeof exceptionGroupSchema>;
export type ExceptionAnalysisResult = z.infer<typeof exceptionAnalysisResultSchema>;
export type DbmsLockWaitEdge = z.infer<typeof dbmsLockWaitEdgeSchema>;
export type DbmsLockAnalysisResult = z.infer<typeof dbmsLockAnalysisResultSchema>;
export type CallEntry = z.infer<typeof callEntrySchema>;
export type CallGroup = z.infer<typeof callGroupSchema>;
export type CallAnalysisResult = z.infer<typeof callAnalysisResultSchema>;

// Элемент списка дел + шапка детали. nullable-поля → omittable() (бэкенд опускает при null).
export const investigationSummarySchema = z.object({
  id: z.string(),
  scenario: investigationScenarioSchema,
  status: investigationStatusSchema,
  startedAtUtc: z.string(),
  stoppedAtUtc: omittable(z.string()),
  startedBy: z.string(),
  stopReason: omittable(investigationStopReasonSchema),
  tenantId: omittable(z.string()),
  infobaseId: omittable(z.string()),
  findingsCount: z.number(),
});

// Пагинированный список дел (конверт {items,total,page,pageSize}). Дел немного (создаются оператором) —
// раздел запрашивает одну страницу с запасом и читает `.items` (зеркаль recordingsPagedSchema).
export const investigationsPagedSchema = z.object({
  items: z.array(investigationSummarySchema),
  total: z.number(),
  page: z.number(),
  pageSize: z.number(),
});

// Снимок включённого сбора (CollectionConfig). Null для исторических дел; nullable-поля опускаются.
export const collectionConfigSchema = z.object({
  logcfgLocation: z.string(),
  events: z.string(),
  durationThresholdMicros: omittable(z.number()),
  processNameFilter: omittable(z.string()),
  format: z.string(),
  historyHours: z.number(),
});

// Одна находка. `result` — разобранный объект анализатора (на проводе вложенный JSON-объект, НЕ строка).
// Типизирован пер-Kind (MLC-243): дискриминант — `kind`. Graceful: если result не распарсился
// (future schemaVersion, неожиданная форма) — result будет null, деталь не падает целиком.
// НЕ используем .strict(): BE может добавить поля; лишние ключи Zod игнорирует.
const findingResultSchema = z
  .union([
    lockAnalysisResultSchema,
    slowQueryAnalysisResultSchema,
    exceptionAnalysisResultSchema,
    dbmsLockAnalysisResultSchema,
    callAnalysisResultSchema,
  ])
  .catch(null as unknown as LockAnalysisResult);

export const findingSchema = z.object({
  kind: findingKindSchema,
  schemaVersion: z.number(),
  result: findingResultSchema,
});

// Деталь дела = шапка + снимок сбора (omittable: null у исторических дел) + находки.
export const investigationDetailSchema = z.object({
  summary: investigationSummarySchema,
  collectionConfig: omittable(collectionConfigSchema),
  findings: z.array(findingSchema),
});

// Серьёзность находки в отчёте (ReportSeverity). FE красит/локализует по строке.
export const reportSeveritySchema = z.enum(["None", "Info", "Warning"]);

// Одна ранжированная находка отчёта: вид + серьёзность + счётчик + готовый русский текст (шаблон по Kind).
export const reportItemSchema = z.object({
  kind: findingKindSchema,
  severity: reportSeveritySchema,
  count: z.number(),
  headline: z.string(),
  recommendation: z.string(),
});

// Отчёт по делу = шапка + ранжированные находки (severity убыванием).
export const reportSchema = z.object({
  summary: investigationSummarySchema,
  generatedAtUtc: z.string(),
  items: z.array(reportItemSchema),
});

// Лёгкий прогресс для поллинга. collectedBytes опускается, если дело не активно / размер не определить.
export const progressSchema = z.object({
  id: z.string(),
  status: investigationStatusSchema,
  startedAtUtc: z.string(),
  elapsedSeconds: z.number(),
  collectedBytes: omittable(z.number()),
});

export type InvestigationStatus = z.infer<typeof investigationStatusSchema>;
export type InvestigationScenario = z.infer<typeof investigationScenarioSchema>;
export type InvestigationStopReason = z.infer<typeof investigationStopReasonSchema>;
export type FindingKind = z.infer<typeof findingKindSchema>;
export type InvestigationSummary = z.infer<typeof investigationSummarySchema>;
export type InvestigationsPaged = z.infer<typeof investigationsPagedSchema>;
export type CollectionConfig = z.infer<typeof collectionConfigSchema>;
export type Finding = z.infer<typeof findingSchema>;
export type InvestigationDetail = z.infer<typeof investigationDetailSchema>;
export type ReportSeverity = z.infer<typeof reportSeveritySchema>;
export type ReportItem = z.infer<typeof reportItemSchema>;
export type InvestigationReport = z.infer<typeof reportSchema>;
export type InvestigationProgress = z.infer<typeof progressSchema>;

// Тело запроса старта (StartInvestigationRequest). infobaseId опционален (задан ⇒ дело привязано к ИБ).
// slowQueryThresholdSeconds (MLC-248) — порог «долгих запросов» В СЕКУНДАХ; релевантен только сценариям
// SlowQueries/GeneralSlow (Мастер шлёт его лишь для них). ≥ 0; не задан ⇒ дефолт 1 c на бэкенде.
export interface StartInvestigationRequest {
  scenario: InvestigationScenario;
  infobaseId?: string | null;
  slowQueryThresholdSeconds?: number;
}
