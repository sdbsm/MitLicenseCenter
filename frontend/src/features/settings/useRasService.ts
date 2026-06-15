import { useQuery } from "@tanstack/react-query";
import { z } from "zod";
import { api } from "@/lib/api";
import { useInvalidatingMutation } from "@/lib/useInvalidatingMutation";
import { omittable } from "@/lib/apiSchema";

/**
 * Zod-схемы и хуки управления службой RAS (MLC-160, ADR-47), поверх BE-контракта
 * MLC-159. Backend опускает null-поля (JsonIgnoreCondition.WhenWritingNull, гоча
 * api-omits-null-fields): nullable-поля статуса (service/target/commandPreview/issue
 * и вложенные best-effort binPath/platformVersion/port) приходят либо со значением,
 * либо ключ отсутствует — НИКОГДА явным null. Поэтому omittable() (принимает оба
 * варианта, нормализует в null), а не z.nullable() (требует ключ).
 *
 * Перф/ленивость (из ревью MLC-159): обнаружение службы на BE перебирает ВСЕ службы
 * Windows + sc qc — дорого. Поэтому /status дёргается ЛЕНИВО (enabled-флаг: только
 * когда блок раскрыт оператором) и кэшируется коротким staleTime, а не на каждом
 * заходе в Настройки.
 */

// Снимок обнаруженной службы RAS. Все поля кроме имени и факта running — best-effort.
export const rasServiceInfoSchema = z.object({
  serviceName: z.string(),
  isRunning: z.boolean(),
  binPath: omittable(z.string()),
  platformVersion: omittable(z.string()),
  port: omittable(z.string()),
});

// Целевые параметры (что хотим видеть в службе): ras.exe выбранной платформы, версия,
// порт (из OneC.RAS.Endpoint), адрес локального агента кластера.
export const rasServiceTargetSchema = z.object({
  rasExePath: z.string(),
  platformVersion: z.string(),
  port: z.string(),
  agentAddress: z.string(),
});

// Ответ GET /ras-service/status. state — одно из 4 значений (см. RasServiceState на BE):
// Ok | NotRegistered | Outdated | Stopped. Держим как string (а не z.enum), чтобы
// будущее BE-состояние не роняло Zod-границу — UI деградирует к «нет действия».
export const rasServiceStatusSchema = z.object({
  state: z.string(),
  service: omittable(rasServiceInfoSchema),
  target: omittable(rasServiceTargetSchema),
  commandPreview: omittable(z.string()),
  targetReady: z.boolean(),
  issue: omittable(z.string()),
});

// Ответ на успешную операцию register/update/start.
export const rasServiceOperationSchema = z.object({
  state: z.string(),
  serviceName: z.string(),
});

export type RasServiceInfo = z.infer<typeof rasServiceInfoSchema>;
export type RasServiceTarget = z.infer<typeof rasServiceTargetSchema>;
export type RasServiceStatus = z.infer<typeof rasServiceStatusSchema>;
export type RasServiceOperationResult = z.infer<typeof rasServiceOperationSchema>;

// Четыре известных состояния. Незнакомое будущее значение → нет лечащего действия.
export type RasServiceState = "Ok" | "NotRegistered" | "Outdated" | "Stopped";
export type RasServiceOperation = "register" | "update" | "start";

export const rasServiceStatusQueryKey = ["ras-service", "status"] as const;

// Кэш ответа: discovery службы на BE дорогое (перебор служб Windows), поэтому держим
// результат «свежим» минуту — повторное раскрытие блока в пределах окна не бьёт по BE.
const STALE_TIME = 60 * 1000;

// Ленивый запрос статуса: enabled передаётся вызывающим (блок раскрыт). До раскрытия
// react-query запрос не выполняет — Настройки открываются без удара по обнаружению.
export function useRasServiceStatus(enabled: boolean) {
  return useQuery({
    queryKey: rasServiceStatusQueryKey,
    queryFn: () => api("/api/v1/ras-service/status", { schema: rasServiceStatusSchema }),
    enabled,
    staleTime: STALE_TIME,
  });
}

// register/update/start. На успехе инвалидируем статус — блок перезапрашивает
// диагностику и показывает новое состояние (после register → Ok/Stopped и т.п.).
export function useRasServiceOperation() {
  return useInvalidatingMutation({
    mutationFn: (operation: RasServiceOperation) =>
      api(`/api/v1/ras-service/${operation}`, {
        method: "POST",
        schema: rasServiceOperationSchema,
      }),
    invalidate: () => [rasServiceStatusQueryKey],
  });
}
