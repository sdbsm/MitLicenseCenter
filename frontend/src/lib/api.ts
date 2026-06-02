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

type UnauthorizedHandler = () => void;
let onUnauthorized: UnauthorizedHandler | null = null;

export function setUnauthorizedHandler(handler: UnauthorizedHandler | null): void {
  onUnauthorized = handler;
}

interface RequestOptions extends Omit<RequestInit, "body"> {
  body?: unknown;
}

export async function api<T>(path: string, options: RequestOptions = {}): Promise<T> {
  const { body, headers, ...rest } = options;

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
