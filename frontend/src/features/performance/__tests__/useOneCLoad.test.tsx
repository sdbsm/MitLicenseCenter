import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { renderHook, waitFor } from "@testing-library/react";
import type { ReactNode } from "react";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { useOneCLoad } from "../useOneCLoad";
import type { OneCLoadSnapshot } from "../types";

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

const sample: OneCLoadSnapshot = {
  capturedAtUtc: "2026-06-08T12:00:00Z",
  sessions: [
    {
      sessionId: "02d5184c-65b5-4d8a-ae39-b156b909fcaf",
      sessionNumber: 1,
      clusterInfobaseId: "22222222-2222-2222-2222-222222222222",
      appId: "1CV8C",
      userName: "Иванов",
      host: "HOST-01",
      process: "487281d5-65b5-4d8a-ae39-b156b909fcaf",
      connection: "b46dead9-1111-2222-3333-444444444444",
      cpuTimeCurrent: 109,
      durationCurrent: 422,
      durationCurrentDbms: 0,
      memoryCurrent: -1_138_560,
      blockedByDbms: 0,
      blockedByLs: 0,
      lastActiveAtUtc: "2026-06-08T20:21:45Z",
    },
  ],
  processes: [
    {
      process: "487281d5-65b5-4d8a-ae39-b156b909fcaf",
      pid: 15876,
      availablePerformance: 416,
      avgCallTime: 1.124,
      memorySize: 1_682_404,
    },
  ],
};

describe("useOneCLoad", () => {
  beforeEach(() => mockedApi.mockReset());

  it("requests the onec-sessions endpoint with a runtime schema (MLC-016 boundary)", async () => {
    mockedApi.mockResolvedValueOnce(sample);

    const { result } = renderHook(() => useOneCLoad(), { wrapper: makeWrapper() });

    await waitFor(() => expect(result.current.isSuccess).toBe(true));
    expect(result.current.data).toEqual(sample);
    expect(mockedApi).toHaveBeenCalledWith(
      "/api/v1/performance/onec-sessions",
      expect.objectContaining({ schema: expect.anything() })
    );
  });

  it("surfaces an error state when the api throws", async () => {
    mockedApi.mockRejectedValueOnce(new Error("boom"));

    const { result } = renderHook(() => useOneCLoad(), { wrapper: makeWrapper() });

    await waitFor(() => expect(result.current.isError).toBe(true));
  });
});
