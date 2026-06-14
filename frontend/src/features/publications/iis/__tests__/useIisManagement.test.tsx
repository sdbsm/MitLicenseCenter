import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { renderHook, waitFor } from "@testing-library/react";
import type { ReactNode } from "react";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { infobasesQueryKey } from "@/features/infobases/useInfobases";
import {
  iisPoolsQueryKey,
  iisServerQueryKey,
  iisSitesQueryKey,
  useIisAppPools,
  useIisServerStatus,
  useIisSites,
  useRecyclePool,
  useResetIis,
  useStartIis,
  useStartPool,
  useStopIis,
} from "../useIisManagement";

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

// FE-11 (MLC-120): хуки управления жизненным циклом IIS (MLC-047). У этих вызовов нет
// Zod-схемы ответа (raw `api<T>()` каст), поэтому покрываем контракт запросов
// (URL/метод/тело confirm) и политику инвалидации после мутаций.
describe("useIisManagement · query hooks", () => {
  beforeEach(() => mockedApi.mockReset());

  it("useIisServerStatus читает /iis/server", async () => {
    mockedApi.mockResolvedValueOnce({ state: "Started", available: true, error: null });
    const { result } = renderHook(() => useIisServerStatus(), {
      wrapper: makeWrapper(makeClient()),
    });

    await waitFor(() => expect(result.current.isSuccess).toBe(true));
    expect(mockedApi).toHaveBeenCalledWith(
      "/api/v1/iis/server",
      expect.objectContaining({ schema: expect.anything() })
    );
    expect(result.current.data?.state).toBe("Started");
  });

  it("useIisAppPools читает /iis/application-pools", async () => {
    mockedApi.mockResolvedValueOnce({ items: [], available: true, error: null });
    const { result } = renderHook(() => useIisAppPools(), { wrapper: makeWrapper(makeClient()) });

    await waitFor(() => expect(result.current.isSuccess).toBe(true));
    expect(mockedApi).toHaveBeenCalledWith(
      "/api/v1/iis/application-pools",
      expect.objectContaining({ schema: expect.anything() })
    );
  });

  it("useIisSites читает /iis/sites", async () => {
    mockedApi.mockResolvedValueOnce({ items: [], available: true, error: null });
    const { result } = renderHook(() => useIisSites(), { wrapper: makeWrapper(makeClient()) });

    await waitFor(() => expect(result.current.isSuccess).toBe(true));
    expect(mockedApi).toHaveBeenCalledWith(
      "/api/v1/iis/sites",
      expect.objectContaining({ schema: expect.anything() })
    );
  });
});

describe("useIisManagement · mutations", () => {
  beforeEach(() => mockedApi.mockReset());

  it("recycle пула шлёт confirm:true (разрушительная операция, серверный гейт)", async () => {
    mockedApi.mockResolvedValueOnce({ name: "AppPool1", state: "Started" });
    const { result } = renderHook(() => useRecyclePool(), { wrapper: makeWrapper(makeClient()) });

    await result.current.mutateAsync("AppPool1");

    expect(mockedApi).toHaveBeenCalledWith("/api/v1/iis/application-pools/recycle", {
      method: "POST",
      body: { name: "AppPool1", confirm: true },
    });
  });

  it("start пула шлёт имя без confirm", async () => {
    mockedApi.mockResolvedValueOnce({ name: "AppPool1", state: "Started" });
    const { result } = renderHook(() => useStartPool(), { wrapper: makeWrapper(makeClient()) });

    await result.current.mutateAsync("AppPool1");

    expect(mockedApi).toHaveBeenCalledWith("/api/v1/iis/application-pools/start", {
      method: "POST",
      body: { name: "AppPool1" },
    });
  });

  it("iisreset (reset) шлёт confirm:true", async () => {
    mockedApi.mockResolvedValueOnce(undefined);
    const { result } = renderHook(() => useResetIis(), { wrapper: makeWrapper(makeClient()) });

    await result.current.mutateAsync();

    expect(mockedApi).toHaveBeenCalledWith("/api/v1/iis/reset", {
      method: "POST",
      body: { confirm: true },
    });
  });

  it("iisreset /stop шлёт confirm:true", async () => {
    mockedApi.mockResolvedValueOnce(undefined);
    const { result } = renderHook(() => useStopIis(), { wrapper: makeWrapper(makeClient()) });

    await result.current.mutateAsync();

    expect(mockedApi).toHaveBeenCalledWith("/api/v1/iis/stop", {
      method: "POST",
      body: { confirm: true },
    });
  });

  it("iisreset /start шлёт POST без confirm (восстановление)", async () => {
    mockedApi.mockResolvedValueOnce(undefined);
    const { result } = renderHook(() => useStartIis(), { wrapper: makeWrapper(makeClient()) });

    await result.current.mutateAsync();

    expect(mockedApi).toHaveBeenCalledWith("/api/v1/iis/start", { method: "POST" });
  });

  it("успешная мутация инвалидирует discovery-ключи IIS и список инфобаз", async () => {
    mockedApi.mockResolvedValueOnce({ name: "AppPool1", state: "Started" });
    const client = makeClient();
    const invalidateSpy = vi.spyOn(client, "invalidateQueries");
    const { result } = renderHook(() => useStartPool(), { wrapper: makeWrapper(client) });

    await result.current.mutateAsync("AppPool1");

    await waitFor(() => {
      expect(invalidateSpy).toHaveBeenCalledWith({ queryKey: iisServerQueryKey });
      expect(invalidateSpy).toHaveBeenCalledWith({ queryKey: iisPoolsQueryKey });
      expect(invalidateSpy).toHaveBeenCalledWith({ queryKey: iisSitesQueryKey });
      expect(invalidateSpy).toHaveBeenCalledWith({ queryKey: infobasesQueryKey });
    });
  });
});
