import { toast } from "sonner";
import { ApiError, readConflictBody } from "@/lib/api";

// MLC-033 — переиспользуемые надстройки над lib/api для обработки ошибок API в
// диалогах. Контракт ConflictBody/readConflictBody не трогаем — здесь только
// классификация поверх них. Паттерн «409 + проверка code → действие» раньше жил
// заинлайненным в каждом диалоге (и точечно как mapConflictToField, MLC-023).

/**
 * Обобщённый классификатор 409-конфликтов. Принимает ошибку и таблицу
 * `code → дескриптор T`; возвращает дескриптор для совпавшего `ConflictBody.code`,
 * если `error` — `ApiError` со `status === 409` и непустым `code`. Иначе `null`,
 * и вызывающая сторона делает свой прежний fallback (toast / иная ветка).
 */
export function matchConflictCode<T>(error: unknown, table: Record<string, T>): T | null {
  if (!(error instanceof ApiError) || error.status !== 409) return null;
  const code = readConflictBody(error)?.code;
  if (!code) return null;
  return table[code] ?? null;
}

/**
 * Общий «хвост» обработки ошибки submit формы после промаха маппинга поля:
 * `400` показывает серверное сообщение (`error.message`), прочее — generic-тост.
 * Дословно совпадал в TenantFormDialog и useInfobaseForm.
 */
export function toastFormSubmitError(error: unknown, t: (key: string) => string): void {
  if (error instanceof ApiError && error.status === 400) {
    toast.error(error.message || t("errors.generic"));
    return;
  }
  toast.error(t("errors.generic"));
}
