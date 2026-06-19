import { z } from "zod";
import { omittable } from "@/lib/apiSchema";

/**
 * Типы и Zod-схемы управления жизненным циклом IIS (MLC-047, ADR-24, MLC-132).
 * State приходит строковым именем IisObjectState (backend JsonStringEnumConverter) —
 * описываем через z.enum + строковую ветку (forward-compatible: незнакомое будущее
 * значение не роняет весь список, деградирует к нейтральному бейджу).
 *
 * Backend опускает null-поля (JsonIgnoreCondition.WhenWritingNull, ADR-32):
 * поле error в IisDiscoveryResponse/IisServerStatus приходит либо строкой,
 * либо отсутствует — объявлено через omittable(), а не nullable().
 */

export const IIS_OBJECT_STATES = ["Unknown", "Starting", "Started", "Stopping", "Stopped"] as const;
export type IisObjectState = (typeof IIS_OBJECT_STATES)[number];

export const iisObjectStateSchema = z
  .enum(IIS_OBJECT_STATES)
  .or(z.string().transform((v) => v as IisObjectState));

// Discovery-элементы (зеркало IisAppPoolDto / IisSiteStateDto бэкенда).
export const iisAppPoolSchema = z.object({
  name: z.string(),
  state: iisObjectStateSchema,
});

export const iisSiteStateSchema = z.object({
  siteName: z.string(),
  state: iisObjectStateSchema,
});

// Общий конверт discovery (зеркало DiscoveryResponse<T> из DiscoveryEndpoints).
// available:false — IIS недоступен/нет прав; error несёт санитизированный текст.
// error=null → бэкенд опускает ключ (WhenWritingNull) → omittable().
export function iisDiscoveryResponseSchema<T>(item: z.ZodType<T>) {
  return z.object({
    items: z.array(item),
    available: z.boolean(),
    error: omittable(z.string()),
  });
}

export const iisAppPoolsResponseSchema = iisDiscoveryResponseSchema(iisAppPoolSchema);
export const iisSitesResponseSchema = iisDiscoveryResponseSchema(iisSiteStateSchema);

// Состояние IIS в целом (служба W3SVC). available:false — статус не прочитан.
// error=null при Available:true — бэкенд опускает ключ → omittable().
export const iisServerStatusSchema = z.object({
  state: iisObjectStateSchema,
  available: z.boolean(),
  error: omittable(z.string()),
});

// Ответ мутации: имя цели + состояние сразу после операции.
export const iisOperationResponseSchema = z.object({
  name: z.string(),
  state: iisObjectStateSchema,
});

export type IisAppPool = z.infer<typeof iisAppPoolSchema>;
export type IisSiteState = z.infer<typeof iisSiteStateSchema>;
export type IisDiscoveryResponse<T> = { items: T[]; available: boolean; error: string | null };
export type IisAppPoolsResponse = z.infer<typeof iisAppPoolsResponseSchema>;
export type IisSitesResponse = z.infer<typeof iisSitesResponseSchema>;
export type IisOperationResponse = z.infer<typeof iisOperationResponseSchema>;
export type IisServerStatus = z.infer<typeof iisServerStatusSchema>;
