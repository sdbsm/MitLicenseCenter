import { describe, it, expect, vi, beforeEach } from "vitest";
import { renderHook, waitFor } from "@testing-library/react";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import type { ReactNode } from "react";
import { useLicenseUsage, useLicenseUsageByTenant } from "../useLicenseUsage";
import type { LicenseUsageSeriesResponse } from "../types";

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

const sample: LicenseUsageSeriesResponse = {
  buckets: [
    { bucketStartUtc: "2026-06-01T00:00:00Z", consumedAvg: 3.5, consumedMax: 5, limit: 10 },
  ],
  fromUtc: "2026-06-01T00:00:00Z",
  toUtc: "2026-06-07T23:59:59Z",
  peakConsumed: 5,
  peakLimit: 10,
  peakAtUtc: "2026-06-01T00:00:00Z",
  averageConsumed: 3.5,
  clamped: false,
  maxSpanDays: 31,
};

describe("useLicenseUsage", () => {
  beforeEach(() => mockedApi.mockReset());

  it("requests the summary endpoint with from/to query params", async () => {
    mockedApi.mockResolvedValueOnce(sample);

    const { result } = renderHook(
      () => useLicenseUsage({ from: "2026-06-01T00:00:00Z", to: "2026-06-07T23:59:59Z" }),
      { wrapper: makeWrapper() }
    );

    await waitFor(() => expect(result.current.isSuccess).toBe(true));
    expect(result.current.data).toEqual(sample);
    expect(mockedApi).toHaveBeenCalledWith(
      "/api/v1/reports/license-usage?from=2026-06-01T00%3A00%3A00Z&to=2026-06-07T23%3A59%3A59Z"
    );
  });

  it("omits the query string when no bounds are set", async () => {
    mockedApi.mockResolvedValueOnce(sample);

    renderHook(() => useLicenseUsage({ from: null, to: null }), { wrapper: makeWrapper() });

    await waitFor(() => expect(mockedApi).toHaveBeenCalledWith("/api/v1/reports/license-usage"));
  });

  it("surfaces an error state when the api throws", async () => {
    mockedApi.mockRejectedValueOnce(new Error("boom"));

    const { result } = renderHook(() => useLicenseUsage({ from: null, to: null }), {
      wrapper: makeWrapper(),
    });

    await waitFor(() => expect(result.current.isError).toBe(true));
  });
});

describe("useLicenseUsageByTenant", () => {
  beforeEach(() => mockedApi.mockReset());

  it("requests the drill-down endpoint with the tenant id in the path", async () => {
    mockedApi.mockResolvedValueOnce(sample);

    const { result } = renderHook(
      () => useLicenseUsageByTenant("tenant-1", { from: null, to: null }),
      { wrapper: makeWrapper() }
    );

    await waitFor(() => expect(result.current.isSuccess).toBe(true));
    expect(mockedApi).toHaveBeenCalledWith("/api/v1/reports/license-usage/tenant-1");
  });

  it("stays disabled (no request) when no tenant is selected", async () => {
    const { result } = renderHook(() => useLicenseUsageByTenant(null, { from: null, to: null }), {
      wrapper: makeWrapper(),
    });

    // enabled:false → query never fetches; data stays undefined and api untouched.
    await waitFor(() => expect(result.current.fetchStatus).toBe("idle"));
    expect(mockedApi).not.toHaveBeenCalled();
    expect(result.current.data).toBeUndefined();
  });
});
