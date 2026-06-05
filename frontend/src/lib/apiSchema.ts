import { z } from "zod";

/**
 * Nullable-поле ответа, которое backend ОПУСКАЕТ при null
 * (`JsonIgnoreCondition.WhenWritingNull`, см. backend `Program.cs`): на wire оно
 * приходит либо со значением, либо ключ отсутствует вовсе — НИКОГДА явным `null`.
 * Поэтому `.nullable()` (требует наличие ключа) ломает валидацию, как только значение
 * пустое. Принимаем оба варианта (отсутствие/`null`) и нормализуем в `null`, чтобы
 * выводимый тип оставался `T | null`, а потребители не различали «null» и «нет ключа».
 */
export function omittable<T extends z.ZodTypeAny>(schema: T) {
  return schema.nullish().transform((value): z.infer<T> | null => value ?? null);
}

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
