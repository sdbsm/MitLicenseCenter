import { z } from "zod";

/**
 * Схема пагинированного ответа-конверта `{ items, total, page, pageSize }`
 * (server-side paging из MLC-015). Generic-фабрика: одна схема конверта
 * переиспользуется всеми пагинированными списками — нужно лишь передать схему
 * элемента. Применяется только на критичных границах (MLC-016).
 */
export function pagedResponseSchema<T>(item: z.ZodType<T>) {
  return z.object({
    items: z.array(item),
    total: z.number(),
    page: z.number(),
    pageSize: z.number(),
  });
}
