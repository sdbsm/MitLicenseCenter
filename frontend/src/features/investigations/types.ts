import { z } from "zod";
import { omittable } from "@/lib/apiSchema";

/**
 * Контракт-слой «Расследование производительности» (трек 1.2, MLC-239, этап C). Зеркалит DTO бэкенда
 * (InvestigationContracts.cs) — Zod-схемы критичной границы (ADR-10.1 / MLC-016) + типы через `z.infer`.
 * Enum'ы приходят строкой (`JsonStringEnumConverter`); строки FE ТОЧНО совпадают с именами членов
 * Domain-enum'ов (frozen-int на проводе уходит именем). Экраны раздела — этап D (MLC-241+); здесь только
 * контракт + хуки + i18n-enum, без UI.
 *
 * `null`-поля бэкенд ОПУСКАЕТ (`JsonIgnoreCondition.WhenWritingNull`): `stoppedAtUtc`/`stopReason`/
 * `tenantId`/`infobaseId` у активного/непривязанного дела не приходят как `null`, а отсутствуют —
 * объявлены через `omittable()` (урок [[api-omits-null-fields]]: `.nullable()` упал бы на отсутствующем
 * ключе). Parity-тесты покрывают omit-null.
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

// Вид находки (FindingKind) — какой анализатор дал результат. Точная пер-Kind типизация `result`
// откладывается на этап D; здесь `result` пермиссивен (см. findingSchema).
export const findingKindSchema = z.enum(["ManagedLocks", "SlowQueries", "Exceptions", "DbmsLocks"]);

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
// ПЕРМИССИВНО `z.unknown()`: точная пер-Kind типизация результата (LockAnalysisResult/SlowQueryAnalysisResult/…)
// откладывается на этап D (MLC-241+) — раздел пока не рендерит детали находок, только число/вид. Схема не
// отвергает ответ из-за неизвестной формы result (она богатая и подлежит стенд-приёмке).
export const findingSchema = z.object({
  kind: findingKindSchema,
  schemaVersion: z.number(),
  result: z.unknown(),
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
export interface StartInvestigationRequest {
  scenario: InvestigationScenario;
  infobaseId?: string | null;
}
