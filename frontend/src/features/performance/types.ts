import { z } from "zod";

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
 */
export const oneCSessionLoadSchema = z.object({
  sessionId: z.string(),
  sessionNumber: z.number().nullable(),
  clusterInfobaseId: z.string(),
  appId: z.string(),
  userName: z.string(),
  host: z.string(),
  process: z.string().nullable(),
  connection: z.string().nullable(),
  cpuTimeCurrent: z.number().nullable(),
  durationCurrent: z.number().nullable(),
  durationCurrentDbms: z.number().nullable(),
  memoryCurrent: z.number().nullable(),
  blockedByDbms: z.number().nullable(),
  blockedByLs: z.number().nullable(),
  lastActiveAtUtc: z.string().nullable(),
});

export const oneCProcessLoadSchema = z.object({
  process: z.string(),
  pid: z.number().nullable(),
  availablePerformance: z.number().nullable(),
  avgCallTime: z.number().nullable(),
  memorySize: z.number().nullable(),
});

export const oneCLoadSnapshotSchema = z.object({
  capturedAtUtc: z.string(),
  sessions: z.array(oneCSessionLoadSchema),
  processes: z.array(oneCProcessLoadSchema),
});

export type OneCSessionLoad = z.infer<typeof oneCSessionLoadSchema>;
export type OneCProcessLoad = z.infer<typeof oneCProcessLoadSchema>;
export type OneCLoadSnapshot = z.infer<typeof oneCLoadSnapshotSchema>;
