import { describe, it, expect, vi, beforeEach, afterEach } from "vitest";
import { z } from "zod";
import {
  api,
  ApiError,
  ApiNetworkError,
  ApiSchemaError,
  readConflictBody,
  setUnauthorizedHandler,
} from "../api";

const originalFetch = globalThis.fetch;

function mockFetch(status: number, body: unknown, contentType = "application/json"): void {
  const headers = new Headers({ "content-type": contentType });
  const text = typeof body === "string" ? body : JSON.stringify(body);
  globalThis.fetch = vi.fn().mockResolvedValue(new Response(text, { status, headers }));
}

describe("api()", () => {
  beforeEach(() => {
    vi.restoreAllMocks();
  });

  afterEach(() => {
    globalThis.fetch = originalFetch;
  });

  it("parses application/problem+json error bodies as JSON (RFC 7807)", async () => {
    // Regression: backend ValidationProblem responses use application/problem+json,
    // which the old check `contentType.includes("application/json")` missed —
    // ApiError.body was then a raw JSON string and form-level error mapping broke.
    mockFetch(
      400,
      {
        type: "https://tools.ietf.org/html/rfc9110#section-15.5.1",
        title: "One or more validation errors occurred.",
        status: 400,
        errors: { NewPassword: ["Новый пароль должен содержать хотя бы один спецсимвол."] },
      },
      "application/problem+json"
    );

    await expect(
      api("/api/v1/auth/change-password", { method: "POST", body: {} })
    ).rejects.toMatchObject({
      status: 400,
      body: {
        errors: { NewPassword: ["Новый пароль должен содержать хотя бы один спецсимвол."] },
      },
    });
  });

  it("parses application/problem+json with charset suffix as JSON", async () => {
    mockFetch(400, { errors: { Value: ["bad"] } }, "application/problem+json; charset=utf-8");

    try {
      await api("/api/v1/settings/x", { method: "PUT", body: { value: "" } });
      expect.fail("expected ApiError");
    } catch (error) {
      expect(error).toBeInstanceOf(ApiError);
      expect((error as ApiError).body).toEqual({ errors: { Value: ["bad"] } });
    }
  });

  it("still parses plain application/json success bodies", async () => {
    mockFetch(200, { ok: true });

    const result = await api<{ ok: boolean }>("/api/v1/anything");
    expect(result).toEqual({ ok: true });
  });

  it("returns text body for non-JSON content types", async () => {
    mockFetch(500, "Internal Server Error", "text/plain");

    try {
      await api("/api/v1/anything");
      expect.fail("expected ApiError");
    } catch (error) {
      expect(error).toBeInstanceOf(ApiError);
      expect((error as ApiError).body).toBe("Internal Server Error");
    }
  });

  it("reject fetch (нет связи) → ApiNetworkError, а не ApiError/сырой TypeError (UX-03)", async () => {
    const cause = new TypeError("Failed to fetch");
    globalThis.fetch = vi.fn().mockRejectedValue(cause);

    try {
      await api("/api/v1/auth/me");
      expect.fail("ожидалась ApiNetworkError");
    } catch (error) {
      expect(error).toBeInstanceOf(ApiNetworkError);
      expect(error).not.toBeInstanceOf(ApiError);
      expect((error as ApiNetworkError).path).toBe("/api/v1/auth/me");
      // Исходная ошибка сохранена в cause для диагностики.
      expect((error as ApiNetworkError).cause).toBe(cause);
    }
  });

  it("401 несёт статус без человекочитаемого/локализованного литерала и зовёт handler", async () => {
    // MLC-017: текст для экрана берётся из i18n на месте показа (LoginPage по
    // status===401), а сам ApiError не должен нести русский литерал «Не авторизован».
    mockFetch(401, "");
    const handler = vi.fn();
    setUnauthorizedHandler(handler);

    try {
      await api("/api/v1/auth/me");
      expect.fail("expected ApiError");
    } catch (error) {
      expect(error).toBeInstanceOf(ApiError);
      const apiError = error as ApiError;
      expect(apiError.status).toBe(401);
      expect(apiError.message).not.toMatch(/[А-Яа-я]/);
      expect(apiError.message).not.toBe("Не авторизован");
      expect(handler).toHaveBeenCalled();
    } finally {
      setUnauthorizedHandler(null);
    }
  });
});

describe("api() с runtime-схемой (MLC-016)", () => {
  beforeEach(() => {
    vi.restoreAllMocks();
  });

  afterEach(() => {
    globalThis.fetch = originalFetch;
  });

  const schema = z.object({ userName: z.string(), roles: z.array(z.string()) });

  it("валидная нагрузка проходит схему и возвращается типизированной", async () => {
    mockFetch(200, { userName: "admin", roles: ["Admin"] });

    const result = await api("/api/v1/auth/me", { schema });
    expect(result).toEqual({ userName: "admin", roles: ["Admin"] });
  });

  it("искажённая нагрузка кидает управляемую ApiSchemaError, а не «тихий» неверный тип", async () => {
    // roles ожидается string[]; backend «разошёлся» и прислал число.
    mockFetch(200, { userName: "admin", roles: 42 });

    try {
      await api("/api/v1/auth/me", { schema });
      expect.fail("ожидалась ApiSchemaError");
    } catch (error) {
      expect(error).toBeInstanceOf(ApiSchemaError);
      expect(error).not.toBeInstanceOf(ApiError);
      expect((error as ApiSchemaError).path).toBe("/api/v1/auth/me");
      // issues несёт исходную ошибку валидации для диагностики.
      expect((error as ApiSchemaError).issues).toBeDefined();
    }
  });

  it("без схемы поведение прежнее: сырой каст без валидации", async () => {
    // Контракт «разошёлся», но схема не передана → as T, без выброса (прежнее поведение).
    mockFetch(200, { userName: "admin", roles: 42 });

    const result = await api<{ userName: string; roles: unknown }>("/api/v1/auth/me");
    expect(result).toEqual({ userName: "admin", roles: 42 });
  });
});

describe("readConflictBody()", () => {
  it("читает тело 409 как ConflictBody", () => {
    const error = new ApiError(409, "conflict", { code: "NAME_DUPLICATE", detail: "x" });
    expect(readConflictBody(error)).toEqual({ code: "NAME_DUPLICATE", detail: "x" });
  });

  it("возвращает null для пустого тела", () => {
    expect(readConflictBody(new ApiError(409, "conflict", null))).toBeNull();
  });
});
