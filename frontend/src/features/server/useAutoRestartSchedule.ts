import { useQuery } from "@tanstack/react-query";
import { z } from "zod";
import { api } from "@/lib/api";
import { omittable } from "@/lib/apiSchema";
import { useInvalidatingMutation } from "@/lib/useInvalidatingMutation";

/**
 * Zod-схема и хуки карточки «Расписание авто-рестартов» во вкладке «Службы» раздела
 * «Сервер» (MLC-218, ADR-55) поверх BE-контракта `/api/v1/server/auto-restart`.
 *
 * Backend опускает null-поля (JsonIgnoreCondition.WhenWritingNull, гоча api-omits-null-fields):
 * lastRunUtc приходит либо со значением (ISO-8601 UTC), либо ключ отсутствует — НИКОГДА
 * явным null. Поэтому omittable() (принимает оба варианта, нормализует в null).
 */

// Текущее расписание: вкл/выкл, время HH:mm (по часам хоста), время последнего прогона
// (UTC, null = ещё не запускалась) и целевые службы (запущенные ragent — что рестартнётся).
export const autoRestartScheduleSchema = z.object({
  enabled: z.boolean(),
  time: z.string(),
  lastRunUtc: omittable(z.string()),
  targetServices: z.array(z.string()),
});

export type AutoRestartSchedule = z.infer<typeof autoRestartScheduleSchema>;

export interface AutoRestartScheduleInput {
  enabled: boolean;
  time: string;
}

export const autoRestartScheduleQueryKey = ["server", "auto-restart"] as const;

// ~30с свежести (как статус сервера) — оперативное наблюдение, не риал-тайм.
const STALE_TIME = 30 * 1000;

export function useAutoRestartSchedule() {
  return useQuery({
    queryKey: autoRestartScheduleQueryKey,
    queryFn: () => api("/api/v1/server/auto-restart", { schema: autoRestartScheduleSchema }),
    staleTime: STALE_TIME,
  });
}

// Сохранение расписания (Admin). На успехе инвалидируем расписание — карточка
// перезапрашивает сохранённое состояние (вкл/выкл + время + цели).
export function useSetAutoRestartSchedule() {
  return useInvalidatingMutation({
    mutationFn: (input: AutoRestartScheduleInput) =>
      api("/api/v1/server/auto-restart", {
        method: "PUT",
        body: input,
        schema: autoRestartScheduleSchema,
      }),
    invalidate: () => [autoRestartScheduleQueryKey],
  });
}
