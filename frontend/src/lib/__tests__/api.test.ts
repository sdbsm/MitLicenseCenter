import { describe, it, expect, vi, beforeEach, afterEach } from "vitest";
import { api, ApiError } from "../api";

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
});
