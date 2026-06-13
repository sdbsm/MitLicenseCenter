import { renderHook } from "@testing-library/react";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { ApiError, ApiNetworkError, ApiSchemaError } from "../api";
import { classifyError, queryClient } from "../queryClient";
import { markOnline, useIsOffline } from "../connectionStatus";

// MLC-121 (UX-03/FE-05) — глобальная классификация ошибок на кэшах React Query
// (classifyError навешен на QueryCache/MutationCache.onError):
//   • ApiNetworkError → markOffline (баннер «нет связи»);
//   • ApiSchemaError  → console.error с распознаваемым префиксом [ApiSchemaError];
//   • прочее (ApiError 4xx/5xx) → ни баннера, ни schema-лога (обрабатывается на месте).
// Тестируем чистую функцию-классификатор против singleton-стора соединения —
// без хрупкой React-Query-плумбинга/сброса модулей.

// Снимок состояния стора через временную подписку (useSyncExternalStore).
function isOffline(): boolean {
  return renderHook(() => useIsOffline()).result.current;
}

describe("classifyError (queryClient onError)", () => {
  beforeEach(() => markOnline());
  afterEach(() => {
    vi.restoreAllMocks();
    markOnline();
  });

  it("ApiSchemaError → console.error с greppable-префиксом [ApiSchemaError], без баннера", () => {
    const spy = vi.spyOn(console, "error").mockImplementation(() => {});
    classifyError(new ApiSchemaError("/api/v1/x", ["bad"]));
    expect(spy).toHaveBeenCalledWith("[ApiSchemaError]", "/api/v1/x", ["bad"]);
    expect(isOffline()).toBe(false);
  });

  it("ApiNetworkError → поднимает баннер «нет связи» (offline=true)", () => {
    classifyError(new ApiNetworkError("/api/v1/x", null));
    expect(isOffline()).toBe(true);
  });

  it("ApiError 4xx/5xx → ни баннера, ни schema-лога (обрабатывается на месте)", () => {
    const spy = vi.spyOn(console, "error").mockImplementation(() => {});
    classifyError(new ApiError(500, "boom", null));
    expect(spy).not.toHaveBeenCalled();
    expect(isOffline()).toBe(false);
  });

  it("queryClient навешивает classifyError на оба кэша", () => {
    expect(queryClient.getQueryCache().config.onError).toBe(classifyError);
    expect(queryClient.getMutationCache().config.onError).toBe(classifyError);
  });
});
