import { z } from "zod";
import { pagedResponseSchema } from "@/lib/apiSchema";

export const infobaseStatusSchema = z.enum(["Active", "Maintenance", "Suspended"]);
export type InfobaseStatus = z.infer<typeof infobaseStatusSchema>;

export const publicationSchema = z.object({
  id: z.string(),
  infobaseId: z.string(),
  siteName: z.string(),
  virtualPath: z.string(),
  platformVersion: z.string(),
  enableOData: z.boolean(),
  enableHttpServices: z.boolean(),
  vrdCustomXml: z.string().nullable(),
  physicalPathOverride: z.string().nullable(),
  createdAt: z.string(),
  updatedAt: z.string().nullable(),
});

export const infobaseSchema = z.object({
  id: z.string(),
  tenantId: z.string(),
  name: z.string(),
  clusterInfobaseId: z.string(),
  databaseServer: z.string(),
  databaseName: z.string(),
  status: infobaseStatusSchema,
  createdAt: z.string(),
  updatedAt: z.string().nullable(),
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

export interface InfobaseDetail {
  infobase: Infobase;
  publication: Publication;
}

export interface ClusterIdAvailability {
  taken: boolean;
  takenByTenantName: string | null;
}

// Тела запросов (не ответы) — runtime-валидация не нужна, оставляем рукописными.
export interface PublicationInput {
  siteName: string;
  virtualPath: string;
  platformVersion: string;
  enableOData: boolean;
  enableHttpServices: boolean;
  vrdCustomXml: string | null;
  physicalPathOverride: string | null;
}

export interface CreateInfobaseInput {
  tenantId: string;
  name: string;
  clusterInfobaseId: string;
  databaseServer: string;
  databaseName: string;
  status: InfobaseStatus;
  publication: PublicationInput;
}

export interface UpdateInfobaseInput {
  name: string;
  clusterInfobaseId: string;
  databaseServer: string;
  databaseName: string;
  status: InfobaseStatus;
  publication: PublicationInput;
}
