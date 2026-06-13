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
 * Тело 400 ValidationProblem (ASP.NET ProblemDetails): словарь `errors` с ключами
 * полей в PascalCase (для вложенной публикации — с префиксом `Publication.`) и
 * массивом сообщений на каждое поле.
 */
interface ValidationProblemBody {
  errors?: Record<string, string[]>;
}

/**
 * Сигнатура `setError`, совместимая с react-hook-form `UseFormSetError<T>`. Generic
 * по имени поля `F` (RHF сужает его до путей конкретной формы), поэтому
 * `form.setError` подставляется напрямую без приведения. Не тянем сам тип RHF в lib
 * (slim-граница): достаточно структурного контракта. `setError` зовётся только с
 * ключами, выведенными из ответа бэка, — соответствие формам обеспечивает `fieldMap`.
 */
type SetFieldError<F extends string = string> = (
  field: F,
  error: { type: string; message: string }
) => void;

/**
 * Нормализует ключ поля из ответа бэка (PascalCase) в имя поля формы (camelCase),
 * сохраняя сегменты вложенного пути: `Publication.SiteName` → `publication.siteName`.
 * Каждый сегмент получает строчную первую букву.
 */
function defaultFieldKey(serverKey: string): string {
  return serverKey
    .split(".")
    .map((seg) => (seg.length > 0 ? seg[0].toLowerCase() + seg.slice(1) : seg))
    .join(".");
}

/**
 * MLC-121 (UX-04) — единый паттерн inline-ошибок форм на 400 ValidationProblem.
 * Если `error` — `ApiError` со `status === 400` и телом-словарём `errors`, маппит
 * ключи полей (PascalCase бэка, в т.ч. с префиксом `Publication.`) на имена полей
 * формы и вызывает `setError(field, …)` ПЕРВЫМ сообщением из массива. `fieldMap`
 * задаёт явное соответствие (server-ключ → имя поля формы); ключи без записи в
 * карте нормализуются first-letter-lowercase-правилом по сегментам.
 *
 * Возвращает `true`, если проставлено хотя бы одно поле; иначе `false` — вызывающий
 * делает прежний fallback (`toastFormSubmitError`). Порядок в формах: 409-code
 * (`matchConflictCode`) → 400-field (`applyFieldErrors`) → generic-тост.
 */
export function applyFieldErrors<F extends string = string>(
  error: unknown,
  setError: SetFieldError<F>,
  fieldMap: Record<string, string> = {}
): boolean {
  if (!(error instanceof ApiError) || error.status !== 400) return false;
  const body = error.body as ValidationProblemBody | null;
  const dict = body?.errors;
  if (!dict || typeof dict !== "object") return false;

  let applied = false;
  for (const [serverKey, messages] of Object.entries(dict)) {
    const message = Array.isArray(messages) ? messages[0] : undefined;
    if (!message) continue;
    const field = fieldMap[serverKey] ?? defaultFieldKey(serverKey);
    // Имя поля выведено из ответа бэка; соответствие путям формы — на совести
    // вызывающего (fieldMap). Каст к F снимает несовместимость с узким RHF-типом.
    setError(field as F, { type: "server", message });
    applied = true;
  }
  return applied;
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
