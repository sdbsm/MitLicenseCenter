import { z } from "zod";
import { omittable, pagedResponseSchema } from "@/lib/apiSchema";

export const infobaseStatusSchema = z.enum(["Active", "Maintenance", "Suspended"]);
export type InfobaseStatus = z.infer<typeof infobaseStatusSchema>;

export const publicationSchema = z.object({
  id: z.string(),
  infobaseId: z.string(),
  siteName: z.string(),
  virtualPath: z.string(),
  platformVersion: z.string(),
  source: z.enum(["Unknown", "Webinst", "Configurator"]),
  physicalPathOverride: omittable(z.string()),
  createdAt: z.string(),
  updatedAt: omittable(z.string()),
  lastCheckStatus: z.enum(["Unknown", "Published", "NotPublished", "Error"]),
  lastCheckAt: omittable(z.string()),
  lastCheckDetails: omittable(z.string()),
  // MLC-151 — токен оптимистической блокировки публикации (SQL Server rowversion → base64).
  // Форма редактирования шлёт его обратно; конкурентный апдейт → 409
  // (PUBLICATION_CONCURRENCY_CONFLICT при самостоятельном PUT, INFOBASE_CONCURRENCY_CONFLICT
  // в составе aggregate-апдейта инфобазы). omit-null ([[api-omits-null-fields]]): под
  // InMemory/до первой записи токена нет — omittable принимает отсутствие/null.
  rowVersion: omittable(z.string()),
});

export const infobaseSchema = z.object({
  id: z.string(),
  tenantId: z.string(),
  name: z.string(),
  clusterInfobaseId: z.string(),
  databaseName: z.string(),
  status: infobaseStatusSchema,
  createdAt: z.string(),
  updatedAt: omittable(z.string()),
  // MLC-151 — токен оптимистической блокировки инфобазы (корень aggregate'а). Форма
  // редактирования шлёт его обратно; устаревший → 409 INFOBASE_CONCURRENCY_CONFLICT.
  // omit-null ([[api-omits-null-fields]]): под InMemory токена нет → omittable.
  rowVersion: omittable(z.string()),
});

export const infobaseListItemSchema = infobaseSchema.extend({
  tenantName: z.string(),
  publication: publicationSchema,
});

/**
 * Список инфобаз (`GET /api/v1/infobases`). Критичная граница (MLC-016):
 * пагинированный конверт со вложенной публикацией валидируется runtime-схемой.
 * Типы выводятся из схем (`z.infer`) — единый источник правды.
 *
 * MLC-150: `clusterAvailable` приходит ТОЛЬКО при фильтре `notInCluster=true`
 * (признак доступности снапшота RAS); BE опускает его (null) в остальных случаях —
 * поэтому `omittable`. `false` ⇒ кластер недоступен, фильтрация не выполнена: UI
 * показывает честное «не удалось проверить кластер», а не пустой «0 найдено».
 */
export const infobaseListResponseSchema = pagedResponseSchema(infobaseListItemSchema).extend({
  clusterAvailable: omittable(z.boolean()),
});

export type Publication = z.infer<typeof publicationSchema>;
export type Infobase = z.infer<typeof infobaseSchema>;
export type InfobaseListItem = z.infer<typeof infobaseListItemSchema>;
export type InfobaseListResponse = z.infer<typeof infobaseListResponseSchema>;

/**
 * Ответ `GET /api/v1/infobases/ids` (MLC-181c) — облегчённый id-набор для bulk-операции
 * «Выбрать все N по фильтру». Критичная граница (валидируется runtime): BE отдаёт только
 * строки, пригодные для bulk (у которых есть публикация), по ТОМУ ЖЕ фильтру, что и список.
 * Поля `items` минимальны — ровно те, что нужны bulk-диалогам для label
 * (`infobaseName — siteName+virtualPath`) и наполнения внешнего выбора (publicationId).
 * `capped=true` ⇒ пригодных строк больше серверного кэпа (`items` усечён, `total` реальный):
 * FE по `capped` НЕ наполняет выбор и просит уточнить фильтр.
 */
export const infobaseBulkIdItemSchema = z.object({
  infobaseId: z.string(),
  publicationId: z.string(),
  infobaseName: z.string(),
  siteName: z.string(),
  virtualPath: z.string(),
});

export const infobaseBulkIdsResponseSchema = z.object({
  items: z.array(infobaseBulkIdItemSchema),
  total: z.number(),
  capped: z.boolean(),
});

export type InfobaseBulkIdItem = z.infer<typeof infobaseBulkIdItemSchema>;
export type InfobaseBulkIdsResponse = z.infer<typeof infobaseBulkIdsResponseSchema>;

/**
 * Zod-схемы ответов detail/availability (MLC-132, FE-09).
 * InfobaseDetailResponse = { infobase: InfobaseResponse, publication: PublicationResponse }.
 * ClusterIdAvailabilityResponse: takenByTenantName=null → ключ отсутствует → omittable().
 */
export const infobaseDetailSchema = z.object({
  infobase: infobaseSchema,
  publication: publicationSchema,
});

export const clusterIdAvailabilitySchema = z.object({
  taken: z.boolean(),
  takenByTenantName: omittable(z.string()),
});

export type InfobaseDetail = z.infer<typeof infobaseDetailSchema>;
export type ClusterIdAvailability = z.infer<typeof clusterIdAvailabilitySchema>;

// Тела запросов (не ответы) — runtime-валидация не нужна, оставляем рукописными.
export interface PublicationInput {
  siteName: string;
  virtualPath: string;
  platformVersion: string;
  physicalPathOverride: string | null;
  // MLC-151 — прочитанный rowversion публикации, отправляемый обратно при редактировании
  // (опционально: при создании отсутствует). Сервер сверяет его как ожидаемую версию.
  rowVersion?: string;
}

export interface CreateInfobaseInput {
  tenantId: string;
  name: string;
  clusterInfobaseId: string;
  databaseName: string;
  status: InfobaseStatus;
  publication: PublicationInput;
}

export interface UpdateInfobaseInput {
  name: string;
  clusterInfobaseId: string;
  databaseName: string;
  status: InfobaseStatus;
  publication: PublicationInput;
  // MLC-151 — прочитанный rowversion инфобазы (корень aggregate'а), отправляемый обратно
  // при редактировании. Рассинхрон → 409 INFOBASE_CONCURRENCY_CONFLICT.
  rowVersion?: string;
}
