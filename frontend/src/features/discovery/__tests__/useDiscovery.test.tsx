import { describe, it, expect, vi, beforeEach } from "vitest";
import { renderHook, waitFor } from "@testing-library/react";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import type { ReactNode } from "react";
import { toDiscoveryState, useDatabases } from "../useDiscovery";

vi.mock("@/lib/api", () => ({
  api: vi.fn(),
}));

import { api } from "@/lib/api";

const mockedApi = vi.mocked(api);

function makeWrapper() {
  const client = new QueryClient({
    defaultOptions: { queries: { retry: false } },
  });
  return ({ children }: { children: ReactNode }) => (
    <QueryClientProvider client={client}>{children}</QueryClientProvider>
  );
}

describe("toDiscoveryState", () => {
  it("treats source as available while loading (no data yet)", () => {
    const state = toDiscoveryState({ data: undefined, isError: false, isFetching: true });
    expect(state).toEqual({ available: true, loading: true, error: null });
  });

  it("passes through available=true from backend", () => {
    const state = toDiscoveryState({
      data: { available: true, error: null },
      isError: false,
      isFetching: false,
    });
    expect(state.available).toBe(true);
    expect(state.error).toBeNull();
  });

  it("marks unavailable with error when backend reports available=false", () => {
    const state = toDiscoveryState({
      data: { available: false, error: "сервер недоступен" },
      isError: false,
      isFetching: false,
    });
    expect(state.available).toBe(false);
    expect(state.error).toBe("сервер недоступен");
  });

  it("marks unavailable on network error", () => {
    const state = toDiscoveryState({ data: undefined, isError: true, isFetching: false });
    expect(state.available).toBe(false);
    expect(state.error).toBe("request-failed");
  });
});

describe("useDatabases", () => {
  beforeEach(() => mockedApi.mockReset());

  it("does not fetch when not enabled", async () => {
    renderHook(() => useDatabases(false), { wrapper: makeWrapper() });
    await new Promise((r) => setTimeout(r, 0));
    expect(mockedApi).not.toHaveBeenCalled();
  });

  // MLC-087: сервер берётся из настройки Sql.Server на бекенде — query-параметра нет.
  it("fetches without a server param when enabled", async () => {
    mockedApi.mockResolvedValueOnce({ items: ["acme_bp"], available: true, error: null });

    const { result } = renderHook(() => useDatabases(true), {
      wrapper: makeWrapper(),
    });

    await waitFor(() => expect(result.current.isSuccess).toBe(true));
    expect(mockedApi).toHaveBeenCalledWith(
      "/api/v1/discovery/databases",
      expect.objectContaining({ schema: expect.anything() })
    );
  });
});
