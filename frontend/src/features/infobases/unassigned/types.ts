import { z } from "zod";
import { omittable } from "@/lib/apiSchema";

// MLC-093 — «нераспределённые» базы кластера 1С (есть в кластере, но не заведены в
// панель и не скрыты оператором). Контракт BE — MLC-092 (UnassignedInfobasesContracts.cs).
// Граница не входит в число критичных Zod-границ (ADR-10.1), но `description` базы
// кластера nullable, а API ОПУСКАЕТ null-поля (урок MLC-067/071) — поэтому валидируем
// схемой с `omittable`, чтобы отсутствие ключа не ломало парсинг и тип оставался `T|null`.
export const unassignedInfobaseItemSchema = z.object({
  clusterInfobaseId: z.string(),
  name: z.string(),
  description: omittable(z.string()),
});

export const hiddenUnassignedInfobaseSchema = z.object({
  clusterInfobaseId: z.string(),
  name: z.string(),
  hiddenAtUtc: z.string(),
  hiddenBy: z.string(),
});

// MLC-095 — обратный дрейф: запись панели, чьего clusterInfobaseId нет в снапшоте кластера
// 1С. Все поля non-null (BE их не опускает), поэтому `omittable` здесь не нужен. Считается
// BE только при available:true — при недоступном RAS приходит пустым.
export const missingInfobaseSchema = z.object({
  infobaseId: z.string(),
  tenantName: z.string(),
  name: z.string(),
  clusterInfobaseId: z.string(),
});

// Available:false ⇒ Items и MissingItems пусты (RAS недоступен), но HiddenItems приходят из
// БД-снапшота — блок «Скрытые» рендерится всегда. CheckedAtUtc — время фактического опроса RAS.
export const unassignedInfobasesResponseSchema = z.object({
  items: z.array(unassignedInfobaseItemSchema),
  hiddenItems: z.array(hiddenUnassignedInfobaseSchema),
  missingItems: z.array(missingInfobaseSchema),
  available: z.boolean(),
  error: omittable(z.string()),
  checkedAtUtc: z.string(),
});

export type UnassignedInfobaseItem = z.infer<typeof unassignedInfobaseItemSchema>;
export type HiddenUnassignedInfobase = z.infer<typeof hiddenUnassignedInfobaseSchema>;
export type MissingInfobase = z.infer<typeof missingInfobaseSchema>;
export type UnassignedInfobasesResponse = z.infer<typeof unassignedInfobasesResponseSchema>;

// Тело hide — снапшот имени базы на момент скрытия (BE проверяет 1..200).
export interface HideUnassignedInfobaseInput {
  clusterInfobaseId: string;
  name: string;
}
