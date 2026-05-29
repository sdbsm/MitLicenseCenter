import { describe, it, expect, vi, beforeEach } from "vitest";
import { renderHook, waitFor } from "@testing-library/react";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import type { ReactNode } from "react";
import { useDashboardSummary } from "../useDashboardSummary";
import type { DashboardSummaryResponse } from "../types";

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

const sampleResponse: DashboardSummaryResponse = {
  tenantsTotal: 2,
  tenantsActive: 2,
  infobasesTotal: 3,
  sessionsActiveTotal: 5,
  licensesConsumedTotal: 5,
  licensesAvailableTotal: 95,
  topTenantsByConsumption: [],
  ras: {
    healthy: true,
    lastCheckedAtUtc: "2026-05-24T12:00:00Z",
    lastErrorMessage: null,
    consecutiveFailures: 0,
  },
};

describe("useDashboardSummary", () => {
  beforeEach(() => {
    mockedApi.mockReset();
  });

  it("returns dashboard summary data on success", async () => {
    mockedApi.mockResolvedValueOnce(sampleResponse);

    const { result } = renderHook(() => useDashboardSummary(), {
      wrapper: makeWrapper(),
    });

    await waitFor(() => expect(result.current.isSuccess).toBe(true));
    expect(result.current.data).toEqual(sampleResponse);
    expect(mockedApi).toHaveBeenCalledWith("/api/v1/dashboard/summary");
  });

  it("surfaces an error state when the api throws", async () => {
    mockedApi.mockRejectedValueOnce(new Error("boom"));

    const { result } = renderHook(() => useDashboardSummary(), {
      wrapper: makeWrapper(),
    });

    await waitFor(() => expect(result.current.isError).toBe(true));
  });
});
