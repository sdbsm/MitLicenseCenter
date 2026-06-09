import { z } from "zod";
import { omittable } from "@/lib/apiSchema";

/**
 * Live-снимок метрик хоста (`GET /api/v1/performance/host`, MLC-064/ADR-26).
 * Критичная граница (ADR-10.1 / MLC-016): это операционный экран реального
 * времени — по `cpu`/`memory`/`disk` и долям семей оператор судит «почему 1С
 * тормозит», поэтому ответ проходит runtime-валидацию схемой. Типы выводятся из
 * схем (`z.infer`) — единый источник правды, зеркалит нейтральные DTO бэкенда.
 *
 * `measuring=true` приходит на первом poll'е: метрики, требующие дельты между
 * двумя замерами (CPU% общий и по процессам, латентность диска, paging), ещё не
 * готовы — фронт показывает «измеряю…», а не рисует нули как реальные значения.
 *
 * Семьи приходят стабильными ключами (`OneC`/`Mssql`/`OsUpdate`/`Antivirus`/
 * `Other`) — локализуются через i18n, русские названия в данных не хардкодятся.
 * Маппинг процесс→семья настраивается оператором (`Performance.ProcessFamilyMap`),
 * поэтому `family` валидируется как свободная строка, а не enum: незнакомый ключ
 * деградирует к подписи «Прочее», но снимок не отвергается.
 */
export const cpuMetricsSchema = z.object({
  totalPercent: z.number(),
  queueLength: z.number(),
});

export const memoryMetricsSchema = z.object({
  availableMBytes: z.number(),
  totalMBytes: z.number(),
  pagesPerSec: z.number(),
});

export const diskMetricsSchema = z.object({
  avgReadSecPerOp: z.number(),
  avgWriteSecPerOp: z.number(),
  queueLength: z.number(),
});

export const processGroupUsageSchema = z.object({
  family: z.string(),
  cpuPercent: z.number(),
  ramBytes: z.number(),
  processCount: z.number(),
});

export const hostMetricsSnapshotSchema = z.object({
  capturedAtUtc: z.string(),
  measuring: z.boolean(),
  cpu: cpuMetricsSchema,
  memory: memoryMetricsSchema,
  disk: diskMetricsSchema,
  processGroups: z.array(processGroupUsageSchema),
  // Сколько процессов бэкенд не смог прочитать из-за нехватки прав (их потребление
  // выпало из атрибуции). `attributionIncomplete` — производный признак бэкенда
  // (`> 0`): под недостаточно привилегированным backend'ом раздел рискует показать
  // ложное «всё Прочее», поэтому фронт рисует честный баннер (MLC-064a).
  processesInaccessible: z.number(),
  attributionIncomplete: z.boolean(),
});

export type CpuMetrics = z.infer<typeof cpuMetricsSchema>;
export type MemoryMetrics = z.infer<typeof memoryMetricsSchema>;
export type DiskMetrics = z.infer<typeof diskMetricsSchema>;
export type ProcessGroupUsage = z.infer<typeof processGroupUsageSchema>;
export type HostMetricsSnapshot = z.infer<typeof hostMetricsSnapshotSchema>;

/**
 * Live-срез нагрузки 1С «кто грузит внутри 1С» (`GET /api/v1/performance/onec-sessions`,
 * MLC-066/067, ADR-26). Та же pull-по-требованию модель, что и host-снимок (~5с poll,
 * ничего не персистится, закрыл вкладку — сбор прекратился). Критичная граница
 * (ADR-10.1 / MLC-016): perf-поля сеанса питают подсветку «кто грузит / заблокирован /
 * завис / молчит», ошибка типа здесь молча исказила бы операционную картину.
 *
 * Все perf-поля **nullable** — на иных версиях/конфигурациях платформы их может не быть,
 * бэкенд «never throws» (ADR-3.3): отсутствие поля → `null`. Фронт отображает `null`
 * как «—», НЕ как `0` (0 — это «вызов завершён / нет нагрузки», а не «неизвестно»).
 *
 * Тонкости из разведки MLC-063: `memoryCurrent` знаковый (отрицателен в момент GC);
 * `process`/`connection` = `null`, когда сеанс не привязан к рабочему процессу (клиент
 * idle); `avgCallTime` дробный (секунды). Имена/инфобазы не резолвятся в этом эндпоинте
 * (он про нагрузку, а не про kill) — идентификация по `userName`/`host`/`appId`.
 *
 * **Сериализация бэкенда опускает `null`-поля целиком** (`JsonIgnoreCondition.WhenWritingNull`):
 * у idle-сеанса `process`/`connection` приходят не как `null`, а отсутствуют вовсе. Поэтому
 * nullable perf-поля объявлены `.nullish()` (nullable + optional) и нормализуются в `null`
 * через `transform` — отсутствие ключа и явный `null` дают единый `number | null` для UI.
 */
const nullableNumber = z
  .number()
  .nullish()
  .transform((v) => v ?? null);
const nullableString = z
  .string()
  .nullish()
  .transform((v) => v ?? null);

export const oneCSessionLoadSchema = z.object({
  sessionId: z.string(),
  sessionNumber: nullableNumber,
  clusterInfobaseId: z.string(),
  appId: z.string(),
  userName: z.string(),
  host: z.string(),
  process: nullableString,
  connection: nullableString,
  cpuTimeCurrent: nullableNumber,
  durationCurrent: nullableNumber,
  durationCurrentDbms: nullableNumber,
  memoryCurrent: nullableNumber,
  blockedByDbms: nullableNumber,
  blockedByLs: nullableNumber,
  lastActiveAtUtc: nullableString,
});

export const oneCProcessLoadSchema = z.object({
  process: z.string(),
  pid: nullableNumber,
  availablePerformance: nullableNumber,
  avgCallTime: nullableNumber,
  memorySize: nullableNumber,
});

export const oneCLoadSnapshotSchema = z.object({
  capturedAtUtc: z.string(),
  sessions: z.array(oneCSessionLoadSchema),
  processes: z.array(oneCProcessLoadSchema),
});

export type OneCSessionLoad = z.infer<typeof oneCSessionLoadSchema>;
export type OneCProcessLoad = z.infer<typeof oneCProcessLoadSchema>;
export type OneCLoadSnapshot = z.infer<typeof oneCLoadSnapshotSchema>;

/**
 * Live-срез нагрузки на MSSQL «1С грузит SQL?» (`GET /api/v1/performance/sql`, MLC-068/069,
 * ADR-26, Фаза 3). Та же pull-по-требованию модель (~5с poll, ничего не персистится). Ответ —
 * `SqlPerformanceView`: `snapshot` (от DMV-пробы) + `databases` (атрибуция база→клиент, её
 * добавляет эндпоинт по своему AppDbContext — vertical slice). Критичная граница (ADR-10.1 /
 * MLC-016): perf-поля питают подсветку «кто грузит / заблокирован».
 *
 * `status` приходит строкой (`JsonStringEnumConverter`): `Ok` — данные сняты; `PermissionDenied`
 * — у учётки backend'а нет `VIEW SERVER STATE`; `Unavailable` — SQL недоступен/строка не настроена.
 * Degraded-статусы несут пустые списки и взводят честный баннер (паттерн MLC-064a).
 *
 * `measuring=true` на первом poll'е: wait-stats и IO-stall кумулятивны с старта SQL и значимы
 * только как дельта между двумя замерами — первый раз дельты ещё нет, фронт показывает «измеряю…».
 * Активные запросы мгновенны и доступны сразу.
 *
 * **Сериализация бэкенда опускает `null`-поля** (`JsonIgnoreCondition.WhenWritingNull`): nullable
 * поля объявлены через `omittable()` (`.nullish()` + нормализация в `null`) — отсутствие ключа и
 * явный `null` дают единый `T | null` для UI. Числовые/строковые perf-поля DMV отдаёт NULL для
 * неактивных частей (например `wait-type` у running). nullable → «—», НЕ `0`.
 */
export const sqlProbeStatusSchema = z.enum(["Ok", "PermissionDenied", "Unavailable"]);

export const sqlActiveRequestSchema = z.object({
  sessionId: z.number(),
  // ≠null → ждёт другой сеанс (звено цепочки блокировок); 0 в DMV бэкенд отдаёт как отсутствие.
  blockingSessionId: omittable(z.number()),
  databaseName: omittable(z.string()),
  // true, когда program_name='1CV83 Server' — признак 1С-originated SQL (MLC-063). Всегда присутствует.
  isOneC: z.boolean(),
  programName: omittable(z.string()),
  hostName: omittable(z.string()),
  status: z.string(),
  waitType: omittable(z.string()),
  waitTimeMs: omittable(z.number()),
  cpuTimeMs: omittable(z.number()),
  elapsedMs: omittable(z.number()),
  logicalReads: omittable(z.number()),
  // Текст запроса, обрезан бэкендом (~1000 симв.); отсутствует у запросов без sql_handle.
  sqlText: omittable(z.string()),
});

export const sqlDatabaseIoSchema = z.object({
  databaseName: omittable(z.string()),
  readStallMsDelta: z.number(),
  writeStallMsDelta: z.number(),
  readsDelta: z.number(),
  writesDelta: z.number(),
});

export const sqlWaitDeltaSchema = z.object({
  waitType: z.string(),
  waitTimeMsDelta: z.number(),
  waitingTasksDelta: z.number(),
});

export const sqlPerformanceSnapshotSchema = z.object({
  capturedAtUtc: z.string(),
  status: sqlProbeStatusSchema,
  measuring: z.boolean(),
  activeRequests: z.array(sqlActiveRequestSchema),
  databaseIo: z.array(sqlDatabaseIoSchema),
  topWaits: z.array(sqlWaitDeltaSchema),
});

// Привязка базы SQL к клиенту панели. tenant*/infobase null, когда базе из DMV не соответствует
// ни одна зарегистрированная инфобаза (master/tempdb, БД панели, незарегистрированная) — строка
// всё равно показывается с клиентом «—». Гранулярность — база (SQL→сеанс→юзер невозможна, ADR-26).
export const sqlDatabaseAttributionSchema = z.object({
  databaseName: z.string(),
  tenantId: omittable(z.string()),
  tenantName: omittable(z.string()),
  infobaseName: omittable(z.string()),
});

export const sqlPerformanceViewSchema = z.object({
  snapshot: sqlPerformanceSnapshotSchema,
  databases: z.array(sqlDatabaseAttributionSchema),
});

export type SqlProbeStatus = z.infer<typeof sqlProbeStatusSchema>;
export type SqlActiveRequest = z.infer<typeof sqlActiveRequestSchema>;
export type SqlDatabaseIo = z.infer<typeof sqlDatabaseIoSchema>;
export type SqlWaitDelta = z.infer<typeof sqlWaitDeltaSchema>;
export type SqlPerformanceSnapshot = z.infer<typeof sqlPerformanceSnapshotSchema>;
export type SqlDatabaseAttribution = z.infer<typeof sqlDatabaseAttributionSchema>;
export type SqlPerformanceView = z.infer<typeof sqlPerformanceViewSchema>;

/**
 * Запись по требованию (Recording, `/api/v1/performance/recordings`, MLC-070/071, ADR-26,
 * Фаза 4). В отличие от трёх live-источников выше, запись **персистится** в БД: оператор
 * включает её вручную для расследования, бэкенд собирает по таймеру и пишет сэмплы, пока не
 * остановят или не сработает авто-стоп. Критичная граница (ADR-10.1 / MLC-016): список и ряд
 * сэмплов питают график host во времени и таблицы топ-виновников за период.
 *
 * `status`/`stopReason` приходят строкой (`JsonStringEnumConverter`). `stoppedAtUtc`/`stopReason`
 * заполнены только у завершённой записи — у `Active` их нет (бэкенд опускает null-поля,
 * `JsonIgnoreCondition.WhenWritingNull`), поэтому объявлены через `omittable()` (урок
 * [[api-omits-null-fields]]: `.nullable()` упал бы на отсутствующем ключе).
 *
 * Сэмпл переиспользует те же формы, что live (`processGroupUsageSchema` / `oneCLoadSnapshotSchema`
 * / `sqlPerformanceSnapshotSchema`) — бэкенд десериализует JSON-колонки обратно в них, фронт
 * разбирает один формат и для live, и для записи. `oneC`/`sql` = null, если в момент сэмпла
 * источник был не настроен/недоступен (best-effort, как live).
 */
export const perfRecordingStatusSchema = z.enum(["Active", "Stopped", "Interrupted"]);
export const perfRecordingStopReasonSchema = z.enum(["Manual", "TimeLimit", "SampleLimit"]);

export const recordingSummarySchema = z.object({
  id: z.string(),
  startedAtUtc: z.string(),
  stoppedAtUtc: omittable(z.string()),
  status: perfRecordingStatusSchema,
  startedBy: z.string(),
  stopReason: omittable(perfRecordingStopReasonSchema),
  sampleCount: z.number(),
});

export const recordingListSchema = z.array(recordingSummarySchema);

export const recordingSampleSchema = z.object({
  sampleUtc: z.string(),
  measuring: z.boolean(),
  cpuPercent: z.number(),
  cpuQueueLength: z.number(),
  memoryAvailableMBytes: z.number(),
  memoryTotalMBytes: z.number(),
  memoryPagesPerSec: z.number(),
  diskAvgReadSecPerOp: z.number(),
  diskAvgWriteSecPerOp: z.number(),
  diskQueueLength: z.number(),
  processesInaccessible: z.number(),
  processGroups: z.array(processGroupUsageSchema),
  oneC: omittable(oneCLoadSnapshotSchema),
  sql: omittable(sqlPerformanceSnapshotSchema),
});

export const recordingDetailSchema = z.object({
  recording: recordingSummarySchema,
  samples: z.array(recordingSampleSchema),
});

export type PerfRecordingStatus = z.infer<typeof perfRecordingStatusSchema>;
export type PerfRecordingStopReason = z.infer<typeof perfRecordingStopReasonSchema>;
export type RecordingSummary = z.infer<typeof recordingSummarySchema>;
export type RecordingSample = z.infer<typeof recordingSampleSchema>;
export type RecordingDetail = z.infer<typeof recordingDetailSchema>;
