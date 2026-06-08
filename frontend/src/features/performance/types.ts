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
