import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { renderHook, waitFor } from "@testing-library/react";
import type { ReactNode } from "react";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { useHostMetrics } from "../useHostMetrics";
import type { HostMetricsSnapshot } from "../types";

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

const sample: HostMetricsSnapshot = {
  capturedAtUtc: "2026-06-08T12:00:00Z",
  measuring: false,
  cpu: { totalPercent: 12, queueLength: 0 },
  memory: { availableMBytes: 8192, totalMBytes: 16384, pagesPerSec: 0 },
  disk: { avgReadSecPerOp: 0.002, avgWriteSecPerOp: 0.003, queueLength: 0 },
  processGroups: [{ family: "OneC", cpuPercent: 8, ramBytes: 1_500_000_000, processCount: 3 }],
  processesInaccessible: 0,
  attributionIncomplete: false,
};

describe("useHostMetrics", () => {
  beforeEach(() => mockedApi.mockReset());

  it("requests the host endpoint with a runtime schema (MLC-016 boundary)", async () => {
    mockedApi.mockResolvedValueOnce(sample);

    const { result } = renderHook(() => useHostMetrics(), { wrapper: makeWrapper() });

    await waitFor(() => expect(result.current.isSuccess).toBe(true));
    expect(result.current.data).toEqual(sample);
    expect(mockedApi).toHaveBeenCalledWith(
      "/api/v1/performance/host",
      expect.objectContaining({ schema: expect.anything() })
    );
  });

  it("surfaces an error state when the api throws", async () => {
    mockedApi.mockRejectedValueOnce(new Error("boom"));

    const { result } = renderHook(() => useHostMetrics(), { wrapper: makeWrapper() });

    await waitFor(() => expect(result.current.isError).toBe(true));
  });
});
