import { z } from "zod";
import { omittable } from "@/lib/apiSchema";
import type { InfobaseListItem } from "@/features/infobases/types";

/**
 * Zod-схемы ответов публикаций (MLC-132, FE-09).
 * Backend опускает null-поля (JsonIgnoreCondition.WhenWritingNull, ADR-32):
 * checkedAt/details/lastCheckAt/lastCheckDetails приходят либо строкой, либо
 * отсутствуют — объявлены через omittable(), а не nullable().
 * Enum-поля (source, lastCheckStatus, status) приходят строкой
 * (JsonStringEnumConverter) — описаны через z.enum + строковую ветку (forward-compatible).
 */

export const PUBLICATION_PUBLISH_STATUSES = [
  "Unknown",
  "Published",
  "NotPublished",
  "Error",
] as const;
export type PublicationPublishStatus = (typeof PUBLICATION_PUBLISH_STATUSES)[number];

export const PUBLICATION_SOURCES = ["Unknown", "Webinst", "Configurator"] as const;
export type PublicationSource = (typeof PUBLICATION_SOURCES)[number];

export const publicationPublishStatusSchema = z
  .enum(PUBLICATION_PUBLISH_STATUSES)
  .or(z.string().transform((v) => v as PublicationPublishStatus));

export const publicationSourceSchema = z
  .enum(PUBLICATION_SOURCES)
  .or(z.string().transform((v) => v as PublicationSource));

/**
 * Ответ проверки/публикации/смены платформы — текущий статус публикации.
 * checkedAt/details = null у ещё не проверенных публикаций → бэкенд опускает ключ.
 */
export const publicationStatusResponseSchema = z.object({
  status: publicationPublishStatusSchema,
  checkedAt: omittable(z.string()),
  details: omittable(z.string()),
});

export type PublicationStatusResponse = z.infer<typeof publicationStatusResponseSchema>;

// Плоское представление публикации — выводится из строки списка инфобаз (useInfobases).
// Не является самостоятельным ответом API — runtime-схема не нужна.
export interface PublicationListItem {
  id: string;
  infobaseId: string;
  infobaseName: string;
  tenantId: string;
  tenantName: string;
  siteName: string;
  virtualPath: string;
  platformVersion: string;
  source: PublicationSource;
  lastCheckStatus: PublicationPublishStatus;
  lastCheckAt: string | null;
  lastCheckDetails: string | null;
}

/**
 * Плоское представление публикации из строки списка инфобаз (MLC-081): после слияния
 * страниц источник данных один — `GET /api/v1/infobases`, а диалоги публикации/смены
 * платформы и bulk-операции продолжают работать с `PublicationListItem`.
 */
export function toPublicationListItem(item: InfobaseListItem): PublicationListItem {
  return {
    id: item.publication.id,
    infobaseId: item.id,
    infobaseName: item.name,
    tenantId: item.tenantId,
    tenantName: item.tenantName,
    siteName: item.publication.siteName,
    virtualPath: item.publication.virtualPath,
    platformVersion: item.publication.platformVersion,
    source: item.publication.source,
    lastCheckStatus: item.publication.lastCheckStatus,
    lastCheckAt: item.publication.lastCheckAt ?? null,
    lastCheckDetails: item.publication.lastCheckDetails ?? null,
  };
}
