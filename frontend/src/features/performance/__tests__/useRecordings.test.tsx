import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { renderHook, waitFor } from "@testing-library/react";
import type { ReactNode } from "react";
import { beforeEach, describe, expect, it, vi } from "vitest";
import {
  recordingsQueryKey,
  useDeleteRecording,
  useRecordingDetail,
  useRecordings,
  useStartRecording,
  useStopRecording,
} from "../useRecordings";
import type { RecordingSummary } from "../types";

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

const summary: RecordingSummary = {
  id: "11111111-1111-1111-1111-111111111111",
  startedAtUtc: "2026-06-09T10:00:00Z",
  stoppedAtUtc: null,
  status: "Active",
  startedBy: "operator",
  stopReason: null,
  sampleCount: 0,
};

describe("useRecordings", () => {
  beforeEach(() => mockedApi.mockReset());

  it("requests a recordings page with a runtime schema (MLC-016 boundary)", async () => {
    const paged = { items: [summary], total: 1, page: 1, pageSize: 100 };
    mockedApi.mockResolvedValueOnce(paged);

    const { result } = renderHook(() => useRecordings(), { wrapper: makeWrapper() });

    await waitFor(() => expect(result.current.isSuccess).toBe(true));
    expect(result.current.data).toEqual(paged);
    expect(mockedApi).toHaveBeenCalledWith(
      "/api/v1/performance/recordings?page=1&pageSize=100",
      expect.objectContaining({ schema: expect.anything() })
    );
  });
});

describe("useRecordingDetail", () => {
  beforeEach(() => mockedApi.mockReset());

  // Регрессия (поймано live-verify): ключ детали НЕ должен коллидировать с ключом списка
  // (`recordingsQueryKey`). Иначе у закрытого диалога (id=null, query disabled) `data` подхватит
  // массив-список из кэша, и `data.recording.status` упадёт (JSX-проп вычисляется при создании
  // элемента, до того как Radix решит не монтировать закрытый Dialog).
  it("disabled (id=null) НЕ подхватывает массив-список из общего кэша", async () => {
    const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
    // заранее наполняем кэш списком (как делает живой useRecordings)
    client.setQueryData(recordingsQueryKey, [summary]);
    const wrapper = ({ children }: { children: ReactNode }) => (
      <QueryClientProvider client={client}>{children}</QueryClientProvider>
    );

    const { result } = renderHook(() => useRecordingDetail(null), { wrapper });

    expect(result.current.data).toBeUndefined();
    expect(mockedApi).not.toHaveBeenCalled();
  });

  it("enabled (id задан) запрашивает деталь по своему пути со схемой", async () => {
    mockedApi.mockResolvedValueOnce({ recording: summary, samples: [] });
    const { result } = renderHook(() => useRecordingDetail("abc"), { wrapper: makeWrapper() });

    await waitFor(() => expect(result.current.isSuccess).toBe(true));
    expect(mockedApi).toHaveBeenCalledWith(
      "/api/v1/performance/recordings/abc",
      expect.objectContaining({ schema: expect.anything() })
    );
  });
});

describe("recording mutations", () => {
  beforeEach(() => mockedApi.mockReset());

  it("start POSTs to the recordings endpoint", async () => {
    mockedApi.mockResolvedValueOnce(summary);
    const { result } = renderHook(() => useStartRecording(), { wrapper: makeWrapper() });

    await result.current.mutateAsync();

    expect(mockedApi).toHaveBeenCalledWith(
      "/api/v1/performance/recordings",
      expect.objectContaining({ method: "POST" })
    );
  });

  it("stop POSTs to the per-id stop endpoint", async () => {
    mockedApi.mockResolvedValueOnce(summary);
    const { result } = renderHook(() => useStopRecording(), { wrapper: makeWrapper() });

    await result.current.mutateAsync("abc");

    expect(mockedApi).toHaveBeenCalledWith(
      "/api/v1/performance/recordings/abc/stop",
      expect.objectContaining({ method: "POST" })
    );
  });

  it("delete DELETEs the per-id endpoint", async () => {
    mockedApi.mockResolvedValueOnce(null);
    const { result } = renderHook(() => useDeleteRecording(), { wrapper: makeWrapper() });

    await result.current.mutateAsync("abc");

    expect(mockedApi).toHaveBeenCalledWith(
      "/api/v1/performance/recordings/abc",
      expect.objectContaining({ method: "DELETE" })
    );
  });
});
