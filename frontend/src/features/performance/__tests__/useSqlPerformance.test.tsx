import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { renderHook, waitFor } from "@testing-library/react";
import type { ReactNode } from "react";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { useSqlPerformance } from "../useSqlPerformance";
import type { SqlPerformanceView } from "../types";

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

const sample: SqlPerformanceView = {
  snapshot: {
    capturedAtUtc: "2026-06-09T10:00:00Z",
    status: "Ok",
    measuring: false,
    activeRequests: [
      {
        sessionId: 77,
        blockingSessionId: null,
        databaseName: "mitpro",
        isOneC: true,
        programName: "1CV83 Server",
        hostName: "ANDREY-PC",
        status: "running",
        waitType: null,
        waitTimeMs: null,
        cpuTimeMs: 120,
        elapsedMs: 340,
        logicalReads: 2048,
        sqlText: "SELECT T1._IDRRef FROM dbo._Reference172 T1",
      },
    ],
    databaseIo: [],
    topWaits: [],
  },
  databases: [
    { databaseName: "mitpro", tenantId: "t1", tenantName: "Клиент", infobaseName: "Бухгалтерия" },
  ],
};

describe("useSqlPerformance", () => {
  beforeEach(() => mockedApi.mockReset());

  it("requests the sql endpoint with a runtime schema (MLC-016 boundary)", async () => {
    mockedApi.mockResolvedValueOnce(sample);

    const { result } = renderHook(() => useSqlPerformance(), { wrapper: makeWrapper() });

    await waitFor(() => expect(result.current.isSuccess).toBe(true));
    expect(result.current.data).toEqual(sample);
    expect(mockedApi).toHaveBeenCalledWith(
      "/api/v1/performance/sql",
      expect.objectContaining({ schema: expect.anything() })
    );
  });

  it("surfaces an error state when the api throws", async () => {
    mockedApi.mockRejectedValueOnce(new Error("boom"));

    const { result } = renderHook(() => useSqlPerformance(), { wrapper: makeWrapper() });

    await waitFor(() => expect(result.current.isError).toBe(true));
  });
});
