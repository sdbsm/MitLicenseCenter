import { z } from "zod";

// MLC-149: рантайм-версия установленной панели. Источник правды — backend
// (`backend/Directory.Build.props` → InformationalVersion), отдаётся анонимным
// liveness-эндпоинтом `/api/v1/health` ({ status, version, utcNow }).
//
// Схема ТОЛЕРАНТНА (как dashboardSummarySchema, FE-19/MLC-120): незнакомые доп.поля
// будущего бэкенда не роняют парс. Нужен только `version` для подвала сайдбара,
// но `status`/`utcNow` объявлены required — это часть документированного контракта.
export const healthSchema = z.object({
  status: z.string(),
  version: z.string(),
  utcNow: z.string(),
});

export type HealthResponse = z.infer<typeof healthSchema>;
