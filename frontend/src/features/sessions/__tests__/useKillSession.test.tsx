import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { renderHook, waitFor } from "@testing-library/react";
import type { ReactNode } from "react";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { sessionsSnapshotQueryKey } from "../useSessionsSnapshot";
import { useKillSession } from "../useKillSession";

vi.mock("@/lib/api", () => ({
  api: vi.fn(),
}));

import { api } from "@/lib/api";

const mockedApi = vi.mocked(api);

function makeClient() {
  return new QueryClient({
    defaultOptions: { queries: { retry: false }, mutations: { retry: false } },
  });
}

function makeWrapper(client: QueryClient) {
  return ({ children }: { children: ReactNode }) => (
    <QueryClientProvider client={client}>{children}</QueryClientProvider>
  );
}

// FE-11 (MLC-120): kill-сессий — POST /api/v1/sessions/{id}/kill, инвалидация снимка
// сеансов. Ответ — пустое тело (api<null>), Zod-схемы у этого вызова нет, поэтому
// проверяем контракт вызова (URL/метод/тело) и инвалидацию ключа снимка.
describe("useKillSession", () => {
  beforeEach(() => mockedApi.mockReset());

  it("POST'ит /sessions/{id}/kill с reason в теле", async () => {
    mockedApi.mockResolvedValueOnce(null);
    const client = makeClient();
    const { result } = renderHook(() => useKillSession(), { wrapper: makeWrapper(client) });

    await result.current.mutateAsync({ id: "sess-1", reason: "over limit" });

    expect(mockedApi).toHaveBeenCalledWith("/api/v1/sessions/sess-1/kill", {
      method: "POST",
      body: { reason: "over limit" },
    });
  });

  it("передаёт reason=undefined, если причина не указана", async () => {
    mockedApi.mockResolvedValueOnce(null);
    const client = makeClient();
    const { result } = renderHook(() => useKillSession(), { wrapper: makeWrapper(client) });

    await result.current.mutateAsync({ id: "sess-2" });

    expect(mockedApi).toHaveBeenCalledWith("/api/v1/sessions/sess-2/kill", {
      method: "POST",
      body: { reason: undefined },
    });
  });

  it("после успеха инвалидирует ключ снимка сеансов", async () => {
    mockedApi.mockResolvedValueOnce(null);
    const client = makeClient();
    const invalidateSpy = vi.spyOn(client, "invalidateQueries");
    const { result } = renderHook(() => useKillSession(), { wrapper: makeWrapper(client) });

    await result.current.mutateAsync({ id: "sess-3", reason: "x" });

    await waitFor(() =>
      expect(invalidateSpy).toHaveBeenCalledWith({ queryKey: sessionsSnapshotQueryKey })
    );
  });

  it("пробрасывает ошибку api наружу (вызывающий маппит 404 в тост)", async () => {
    mockedApi.mockRejectedValueOnce(new Error("boom"));
    const client = makeClient();
    const { result } = renderHook(() => useKillSession(), { wrapper: makeWrapper(client) });

    await expect(result.current.mutateAsync({ id: "sess-4" })).rejects.toThrow("boom");
  });
});
