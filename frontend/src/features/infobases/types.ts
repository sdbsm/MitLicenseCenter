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
});

export const infobaseListItemSchema = infobaseSchema.extend({
  tenantName: z.string(),
  publication: publicationSchema,
});

/**
 * Список инфобаз (`GET /api/v1/infobases`). Критичная граница (MLC-016):
 * пагинированный конверт со вложенной публикацией валидируется runtime-схемой.
 * Типы выводятся из схем (`z.infer`) — единый источник правды.
 */
export const infobaseListResponseSchema = pagedResponseSchema(infobaseListItemSchema);

export type Publication = z.infer<typeof publicationSchema>;
export type Infobase = z.infer<typeof infobaseSchema>;
export type InfobaseListItem = z.infer<typeof infobaseListItemSchema>;
export type InfobaseListResponse = z.infer<typeof infobaseListResponseSchema>;

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
}
