import { describe, it, expect, vi, beforeEach } from "vitest";
import { renderHook, waitFor } from "@testing-library/react";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import type { ReactNode } from "react";
import { useHealth } from "../useHealth";
import type { HealthResponse } from "../types";

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

const sampleResponse: HealthResponse = {
  status: "ok",
  version: "0.4.0-beta",
  utcNow: "2026-06-14T08:00:00Z",
};

describe("useHealth", () => {
  beforeEach(() => {
    mockedApi.mockReset();
  });

  it("returns health data (version) on success", async () => {
    mockedApi.mockResolvedValueOnce(sampleResponse);

    const { result } = renderHook(() => useHealth(), { wrapper: makeWrapper() });

    await waitFor(() => expect(result.current.isSuccess).toBe(true));
    expect(result.current.data?.version).toBe("0.4.0-beta");
    // Запрос несёт runtime-схему (Zod-граница) на анонимный liveness.
    expect(mockedApi).toHaveBeenCalledWith(
      "/api/v1/health",
      expect.objectContaining({ schema: expect.anything() })
    );
  });

  it("surfaces an error state when /health is unavailable", async () => {
    // Хук объявляет `retry: 1`, поэтому к ошибке приходит после одного ретрая —
    // оба вызова отклоняем и даём waitFor запас по времени.
    mockedApi.mockRejectedValue(new Error("network down"));

    const { result } = renderHook(() => useHealth(), { wrapper: makeWrapper() });

    await waitFor(() => expect(result.current.isError).toBe(true), { timeout: 5000 });
    // data остаётся undefined — потребитель (Sidebar) скрывает строку версии.
    expect(result.current.data).toBeUndefined();
  });
});
