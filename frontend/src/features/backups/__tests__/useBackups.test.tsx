import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { renderHook, waitFor } from "@testing-library/react";
import type { ReactNode } from "react";
import { beforeEach, describe, expect, it, vi } from "vitest";
import type { BackupSummary } from "../types";
import { backupsQueryKey, useBackups, useDeleteBackup, useStartBackup } from "../useBackups";

vi.mock("@/lib/api", () => ({
  api: vi.fn(),
}));

import { api } from "@/lib/api";

const mockedApi = vi.mocked(api);

function makeWrapper() {
  const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return ({ children }: { children: ReactNode }) => (
    <QueryClientProvider client={client}>{children}</QueryClientProvider>
  );
}

const infobaseId = "22222222-2222-2222-2222-222222222222";

const summary: BackupSummary = {
  id: "11111111-1111-1111-1111-111111111111",
  infobaseId,
  databaseServer: "(local)",
  databaseName: "acme_bp",
  status: "Queued",
  requestedBy: "operator",
  requestedAtUtc: "2026-06-10T08:00:00Z",
  startedAtUtc: null,
  completedAtUtc: null,
  filePath: null,
  fileSizeBytes: null,
  failureReason: "None",
  errorMessage: null,
  fileAvailable: null,
};

describe("useBackups", () => {
  beforeEach(() => mockedApi.mockReset());

  it("запрашивает страницу бэкапов инфобазы с runtime-схемой (граница MLC-016)", async () => {
    const paged = { items: [summary], total: 1, page: 1, pageSize: 100 };
    mockedApi.mockResolvedValueOnce(paged);

    const { result } = renderHook(() => useBackups(infobaseId), { wrapper: makeWrapper() });

    await waitFor(() => expect(result.current.isSuccess).toBe(true));
    expect(result.current.data).toEqual(paged);
    expect(mockedApi).toHaveBeenCalledWith(
      `/api/v1/backups?infobaseId=${infobaseId}&page=1&pageSize=100`,
      expect.objectContaining({ schema: expect.anything() })
    );
  });

  // Ключ детали обособлен per-infobase (урок MLC-071: общий ключ подсовывал закрытому
  // диалогу чужие данные); disabled-запрос (id=null) не должен ходить в сеть и не должен
  // подхватывать список другой базы из кэша.
  it("disabled (id=null) не запрашивает и не подхватывает чужой кэш", () => {
    const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
    client.setQueryData(backupsQueryKey(infobaseId), [summary]);
    const wrapper = ({ children }: { children: ReactNode }) => (
      <QueryClientProvider client={client}>{children}</QueryClientProvider>
    );

    const { result } = renderHook(() => useBackups(null), { wrapper });

    expect(result.current.data).toBeUndefined();
    expect(mockedApi).not.toHaveBeenCalled();
  });
});

describe("backup mutations", () => {
  beforeEach(() => mockedApi.mockReset());

  it("start POST'ит { infobaseId } на /backups со схемой ответа", async () => {
    mockedApi.mockResolvedValueOnce(summary);
    const { result } = renderHook(() => useStartBackup(), { wrapper: makeWrapper() });

    await result.current.mutateAsync(infobaseId);

    expect(mockedApi).toHaveBeenCalledWith(
      "/api/v1/backups",
      expect.objectContaining({
        method: "POST",
        body: { infobaseId },
        schema: expect.anything(),
      })
    );
  });

  it("delete DELETE'ит per-id эндпоинт", async () => {
    mockedApi.mockResolvedValueOnce(null);
    const { result } = renderHook(() => useDeleteBackup(), { wrapper: makeWrapper() });

    await result.current.mutateAsync({ id: "abc", infobaseId });

    expect(mockedApi).toHaveBeenCalledWith(
      "/api/v1/backups/abc",
      expect.objectContaining({ method: "DELETE" })
    );
  });
});
