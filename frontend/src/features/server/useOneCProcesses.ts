import { useQuery } from "@tanstack/react-query";
import { z } from "zod";
import { api } from "@/lib/api";
import { omittable } from "@/lib/apiSchema";
import { useInvalidatingMutation } from "@/lib/useInvalidatingMutation";

/**
 * Zod-схемы и хуки блока «Рабочие процессы 1С» вкладки «Службы» раздела «Сервер»
 * (MLC-219/220, ADR-54/56) поверх BE-контракта `/api/v1/server/onec/processes`. Live-снимок
 * рабочих процессов (`rphost`) через `rac process list` (Viewer) + мягкий рестарт по Pid
 * (`POST .../restart`, Admin + confirm, MLC-220): у `rac` нет «restart process» → рестарт =
 * завершение ОС-процесса rphost по Pid, кластер 1С авто-поднимает новый.
 *
 * Backend опускает null-поля (JsonIgnoreCondition.WhenWritingNull, гоча api-omits-null-fields):
 * pid / availablePerformance / avgCallTime / memorySize приходят либо со значением, либо ключ
 * отсутствует — НИКОГДА явным null. Поэтому omittable() (принимает оба варианта, нормализует
 * в null). Отсутствующее perf-поле UI рисует как «—», НЕ как 0 (парсер «never throws»: на иных
 * версиях платформы поля может не быть).
 */

// Один рабочий процесс кластера 1С. process — UUID рабочего процесса; avgCallTime — средняя
// длительность вызова в секундах (дробная); memorySize — занятая память в байтах.
export const oneCProcessSchema = z.object({
  process: z.string(),
  pid: omittable(z.number()),
  availablePerformance: omittable(z.number()),
  avgCallTime: omittable(z.number()),
  memorySize: omittable(z.number()),
});

// Снимок процессов. В degraded-ветке (rac недоступен/не настроен) processes пуст — экран
// показывает «процессов нет», а не падает.
export const oneCProcessesSchema = z.object({
  processes: z.array(oneCProcessSchema),
});

export type OneCProcess = z.infer<typeof oneCProcessSchema>;
export type OneCProcesses = z.infer<typeof oneCProcessesSchema>;

export const oneCProcessesQueryKey = ["server", "onec", "processes"] as const;

// ~30с свежести (как статус сервера) — оперативное наблюдение, не риал-тайм.
const STALE_TIME = 30 * 1000;

export function useOneCProcesses() {
  return useQuery({
    queryKey: oneCProcessesQueryKey,
    queryFn: () => api("/api/v1/server/onec/processes", { schema: oneCProcessesSchema }),
    staleTime: STALE_TIME,
  });
}

// Ответ рестарта rphost: завершённый Pid + исход строкой ("Restarted" — успех/идемпотентность;
// guard/таймаут приходят 404/409 и в этот успешный путь не попадают).
export const oneCProcessRestartSchema = z.object({
  pid: z.number(),
  outcome: z.string(),
});

export type OneCProcessRestartResult = z.infer<typeof oneCProcessRestartSchema>;

// Рестарт рабочего процесса 1С по Pid (Admin + confirm, MLC-220). На успехе инвалидируем
// список процессов — таблица перезапрашивает снимок (старый Pid исчезнет, появится новый).
export function useRestartOneCProcess() {
  return useInvalidatingMutation({
    mutationFn: (pid: number) =>
      api("/api/v1/server/onec/processes/restart", {
        method: "POST",
        body: { pid, confirm: true },
        schema: oneCProcessRestartSchema,
      }),
    invalidate: () => [oneCProcessesQueryKey],
  });
}
