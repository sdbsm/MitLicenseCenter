import { useQuery } from "@tanstack/react-query";
import { z } from "zod";
import { api } from "@/lib/api";
import { omittable } from "@/lib/apiSchema";

/**
 * Zod-схема и хук блока «Планы обслуживания» вкладки «Обслуживание» раздела «Сервер»
 * (MLC-217, ADR-54) поверх BE-контракта `GET /api/v1/server/maintenance/plans`. Live-read
 * планов обслуживания SQL из msdb.dbo.sysmaintplan_* + истории заданий SQL Agent (только
 * чтение, без своих таблиц/джоб).
 *
 * Backend опускает null-поля (JsonIgnoreCondition.WhenWritingNull, гоча api-omits-null-fields):
 * lastRunUtc/durationSeconds приходят либо со значением, либо ключ отсутствует — НИКОГДА явным
 * null. Поэтому omittable() (принимает оба варианта, нормализует в null).
 *
 * status/outcome держим как z.string() (а не z.enum), чтобы будущий BE-статус не ронял границу.
 */

// Один шаг последнего прогона под-плана (что делалось + успех).
export const maintenanceTaskSchema = z.object({
  detail: z.string(),
  succeeded: z.boolean(),
});

// Под-план: имя, «по расписанию», итог последнего прогона строкой
// (Succeeded/Failed/Overdue/NeverRun/Unknown), время прогона, длительность, детализация по шагам.
export const maintenanceSubplanSchema = z.object({
  name: z.string(),
  hasSchedule: z.boolean(),
  outcome: z.string(),
  lastRunUtc: omittable(z.string()),
  durationSeconds: omittable(z.number()),
  tasks: z.array(maintenanceTaskSchema),
});

// План обслуживания с под-планами.
export const maintenancePlanSchema = z.object({
  name: z.string(),
  subplans: z.array(maintenanceSubplanSchema),
});

// Снимок планов. status — string ради forward-compat (Ok/AgentUnavailable/PermissionDenied/
// Unavailable сейчас). В degraded-ветках plans пуст.
export const maintenancePlansSchema = z.object({
  status: z.string(),
  plans: z.array(maintenancePlanSchema),
});

export type MaintenanceTask = z.infer<typeof maintenanceTaskSchema>;
export type MaintenanceSubplan = z.infer<typeof maintenanceSubplanSchema>;
export type MaintenancePlan = z.infer<typeof maintenancePlanSchema>;
export type MaintenancePlans = z.infer<typeof maintenancePlansSchema>;

export const maintenancePlansQueryKey = ["server", "maintenance", "plans"] as const;

// Итоги под-плана, на которые поднимается сигнал «обслуживание требует внимания» (для алерта
// на «Обзоре» и подсветки блока). Совпадает с SubplanRunPolicy.IsAlerting на бэкенде.
export const alertingOutcomes = new Set(["Failed", "Overdue"]);

// ~30с свежести (как статус сервера) — оперативное наблюдение, не риал-тайм.
const STALE_TIME = 30 * 1000;

export function useMaintenancePlans() {
  return useQuery({
    queryKey: maintenancePlansQueryKey,
    queryFn: () => api("/api/v1/server/maintenance/plans", { schema: maintenancePlansSchema }),
    staleTime: STALE_TIME,
  });
}
