import { useQuery } from "@tanstack/react-query";
import { z } from "zod";
import { api } from "@/lib/api";
import { useInvalidatingMutation } from "@/lib/useInvalidatingMutation";
import { omittable } from "@/lib/apiSchema";

/**
 * Zod-схемы и хуки раздела «Сервер» (MLC-214, ADR-54/55) поверх BE-контракта
 * MLC-213 (`/api/v1/server/*`). Backend опускает null-поля
 * (JsonIgnoreCondition.WhenWritingNull, гоча api-omits-null-fields): nullable-поля
 * (platformVersion / serviceName / instance / error) приходят либо со значением,
 * либо ключ отсутствует — НИКОГДА явным null. Поэтому omittable() (принимает оба
 * варианта, нормализует в null), а не z.nullable() (требует ключ).
 *
 * overall / state держим как z.string() (а не z.enum), чтобы будущее BE-состояние
 * не роняло Zod-границу — UI деградирует к нейтральному варианту.
 */

// Одна служба сервера 1С (ragent). platformVersion — best-effort из ImagePath.
export const oneCServerSchema = z.object({
  serviceName: z.string(),
  running: z.boolean(),
  platformVersion: omittable(z.string()),
});

// Сводка RAS (только наблюдение — управление в «Параметрах» / /ras-service/*).
export const rasStatusSchema = z.object({
  state: z.string(),
  running: z.boolean(),
  serviceName: omittable(z.string()),
  available: z.boolean(),
  error: omittable(z.string()),
});

// Сводка локальной службы SQL (только наблюдение, ADR-54).
export const sqlStatusSchema = z.object({
  instance: omittable(z.string()),
  serviceName: omittable(z.string()),
  running: z.boolean(),
  available: z.boolean(),
  error: omittable(z.string()),
});

// Сводка IIS (только наблюдение — управление в /api/v1/iis/*).
export const iisStatusSchema = z.object({
  state: z.string(),
  available: z.boolean(),
  error: omittable(z.string()),
});

// Сводный статус узла. overall — string ради forward-compat (Healthy/Degraded/Down/Unknown
// сейчас, но новое значение не должно ронять границу).
export const serverStatusSchema = z.object({
  oneCServers: z.array(oneCServerSchema),
  ras: rasStatusSchema,
  sql: sqlStatusSchema,
  iis: iisStatusSchema,
  overall: z.string(),
});

// Ответ мутации сервера 1С: имя службы + итоговое верифицированное состояние
// ("Running"/"Stopped").
export const serverOperationSchema = z.object({
  serviceName: z.string(),
  finalStatus: z.string(),
});

export type OneCServer = z.infer<typeof oneCServerSchema>;
export type RasStatus = z.infer<typeof rasStatusSchema>;
export type SqlStatus = z.infer<typeof sqlStatusSchema>;
export type IisStatus = z.infer<typeof iisStatusSchema>;
export type ServerStatus = z.infer<typeof serverStatusSchema>;
export type ServerOperationResult = z.infer<typeof serverOperationSchema>;

// Операции над службой сервера 1С. start — без Confirm (не разрушителен);
// stop/restart — с Confirm (прерывают работу всех баз узла).
export type OneCServerOperation = "start" | "stop" | "restart";

export interface OneCServerOperationInput {
  operation: OneCServerOperation;
  serviceName: string;
  confirm?: boolean;
}

export const serverStatusQueryKey = ["server", "status"] as const;

// Кэш ответа: статус узла делят дашборд-плашка и экран «Сервер» — один queryKey,
// чтобы не бить BE дважды. ~30с свежести достаточно для оперативного наблюдения.
const STALE_TIME = 30 * 1000;

export function useServerStatus() {
  return useQuery({
    queryKey: serverStatusQueryKey,
    queryFn: () => api("/api/v1/server/status", { schema: serverStatusSchema }),
    staleTime: STALE_TIME,
  });
}

// start/stop/restart. На успехе инвалидируем статус — экран и плашка перезапрашивают
// сводку и показывают новое состояние службы.
export function useOneCServerOperation() {
  return useInvalidatingMutation({
    mutationFn: ({ operation, serviceName, confirm }: OneCServerOperationInput) =>
      api(`/api/v1/server/onec/${operation}`, {
        method: "POST",
        body: operation === "start" ? { serviceName } : { serviceName, confirm },
        schema: serverOperationSchema,
      }),
    invalidate: () => [serverStatusQueryKey],
  });
}
