export class ApiError extends Error {
  public readonly status: number;
  public readonly body: unknown;

  constructor(status: number, message: string, body: unknown) {
    super(message);
    this.name = "ApiError";
    this.status = status;
    this.body = body;
  }
}

/**
 * Форма тела ответа 409 Conflict: машиночитаемый `code` (для маппинга в
 * локализованную ошибку поля формы) и необязательный `detail`. Единое
 * определение для всех диалогов — раньше дублировалось в каждом из них.
 */
export interface ConflictBody {
  code?: string;
  detail?: string;
}

/**
 * Читает `error.body` как `ConflictBody`. Возвращает `null`, если тело пустое.
 * Вызывать после проверки `error.status === 409`.
 */
export function readConflictBody(error: ApiError): ConflictBody | null {
  return (error.body as ConflictBody | null) ?? null;
}

/**
 * Минимальный контракт схемы-валидатора ответа (структурно совместим с zod
 * `ZodType`). `lib/api` намеренно НЕ зависит от zod — сами схемы живут рядом с
 * типами фичей (`features/<feature>/types.ts`); сюда они попадают только через
 * этот узкий интерфейс.
 */
export interface ResponseSchema<T> {
  parse(data: unknown): T;
}

/**
 * Ответ получен успешно (2xx), но не прошёл runtime-валидацию схемой: контракт
 * backend разошёлся с ожидаемым FE-типом. Управляемая ошибка вместо «тихого»
 * неверного типа (ADR-10.1 → MLC-016). Бросается только на КРИТИЧНЫХ границах,
 * где вызывающий передал `schema`; на остальных эндпоинтах поведение не меняется.
 */
export class ApiSchemaError extends Error {
  public readonly path: string;
  public readonly issues: unknown;

  constructor(path: string, issues: unknown) {
    super(`Ответ ${path} не соответствует ожидаемой схеме API`);
    this.name = "ApiSchemaError";
    this.path = path;
    this.issues = issues;
  }
}

type UnauthorizedHandler = () => void;
let onUnauthorized: UnauthorizedHandler | null = null;

export function setUnauthorizedHandler(handler: UnauthorizedHandler | null): void {
  onUnauthorized = handler;
}

interface RequestOptions<T> extends Omit<RequestInit, "body"> {
  body?: unknown;
  /**
   * Необязательная runtime-схема ответа. Если передана — распарсенный JSON
   * валидируется ею (а не «кастится вслепую» через `as T`). Включается только
   * на критичных границах (auth/me, sessions snapshot, пагинированные списки —
   * MLC-016); остальные вызовы оставляют прежний `payload as T`.
   */
  schema?: ResponseSchema<T>;
}

export async function api<T>(path: string, options: RequestOptions<T> = {}): Promise<T> {
  const { body, headers, schema, ...rest } = options;

  const init: RequestInit = {
    credentials: "include",
    ...rest,
    headers: {
      Accept: "application/json",
      ...(body !== undefined ? { "Content-Type": "application/json" } : {}),
      ...headers,
    },
    body: body !== undefined ? JSON.stringify(body) : undefined,
  };

  const response = await fetch(path, init);

  if (response.status === 401) {
    onUnauthorized?.();
    // Без человекочитаемого литерала: статус 401 несёт сам сигнал, а текст для
    // экрана берётся из i18n на месте показа (например, LoginPage по status===401).
    throw new ApiError(401, "HTTP 401", null);
  }

  const contentType = response.headers.get("content-type") ?? "";
  const isJson = contentType.includes("application/json") || contentType.includes("+json");
  const payload: unknown = isJson
    ? await response.json().catch(() => null)
    : await response.text().catch(() => null);

  if (!response.ok) {
    const message = extractMessage(payload) ?? `HTTP ${response.status}`;
    throw new ApiError(response.status, message, payload);
  }

  if (schema) {
    try {
      return schema.parse(payload);
    } catch (issues) {
      // Управляемый сбой границы вместо «тихого» неверного типа: backend-контракт
      // разошёлся с ожидаемым FE-типом (MLC-016 / ADR-10.1).
      throw new ApiSchemaError(path, issues);
    }
  }

  return payload as T;
}

function extractMessage(payload: unknown): string | null {
  if (typeof payload === "string" && payload.length > 0) {
    return payload;
  }
  if (payload && typeof payload === "object") {
    const obj = payload as Record<string, unknown>;
    if (typeof obj.detail === "string") return obj.detail;
    if (typeof obj.title === "string") return obj.title;
    if (typeof obj.message === "string") return obj.message;
  }
  return null;
}
