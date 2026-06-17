import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { renderHook, waitFor } from "@testing-library/react";
import type { ReactNode } from "react";
import { beforeEach, describe, expect, it, vi } from "vitest";
import type { DatabaseSizeSeriesResponse, DatabaseSizeTenantSeriesResponse } from "../types";
import { useDatabaseSize, useDatabaseSizeByTenant } from "../useDatabaseSize";

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

const summarySample: DatabaseSizeSeriesResponse = {
  points: [{ atUtc: "2026-06-01T00:00:00Z", dataBytes: 100, logBytes: 20, totalBytes: 120 }],
  tenants: [
    {
      tenantId: "t1",
      tenantName: "Acme",
      dataBytes: 100,
      logBytes: 20,
      totalBytes: 120,
      databaseCount: 2,
    },
  ],
  fromUtc: "2026-06-01T00:00:00Z",
  toUtc: "2026-06-07T23:59:59Z",
  clamped: false,
  maxSpanDays: 31,
};

const tenantSample: DatabaseSizeTenantSeriesResponse = {
  points: [{ atUtc: "2026-06-01T00:00:00Z", dataBytes: 100, logBytes: 20, totalBytes: 120 }],
  databases: [{ databaseName: "acme_db", dataBytes: 100, logBytes: 20, totalBytes: 120 }],
  fromUtc: "2026-06-01T00:00:00Z",
  toUtc: "2026-06-07T23:59:59Z",
  clamped: false,
  maxSpanDays: 31,
};

describe("useDatabaseSize", () => {
  beforeEach(() => mockedApi.mockReset());

  it("requests the summary endpoint with from/to query params", async () => {
    mockedApi.mockResolvedValueOnce(summarySample);

    const { result } = renderHook(
      () => useDatabaseSize({ from: "2026-06-01T00:00:00Z", to: "2026-06-07T23:59:59Z" }),
      { wrapper: makeWrapper() }
    );

    await waitFor(() => expect(result.current.isSuccess).toBe(true));
    expect(result.current.data).toEqual(summarySample);
    expect(mockedApi).toHaveBeenCalledWith(
      "/api/v1/reports/database-size?from=2026-06-01T00%3A00%3A00Z&to=2026-06-07T23%3A59%3A59Z",
      expect.objectContaining({ schema: expect.anything() })
    );
  });

  it("omits the query string when no bounds are set", async () => {
    mockedApi.mockResolvedValueOnce(summarySample);

    renderHook(() => useDatabaseSize({ from: null, to: null }), { wrapper: makeWrapper() });

    await waitFor(() =>
      expect(mockedApi).toHaveBeenCalledWith(
        "/api/v1/reports/database-size",
        expect.objectContaining({ schema: expect.anything() })
      )
    );
  });

  it("surfaces an error state when the api throws", async () => {
    mockedApi.mockRejectedValueOnce(new Error("boom"));

    const { result } = renderHook(() => useDatabaseSize({ from: null, to: null }), {
      wrapper: makeWrapper(),
    });

    await waitFor(() => expect(result.current.isError).toBe(true));
  });
});

describe("useDatabaseSizeByTenant", () => {
  beforeEach(() => mockedApi.mockReset());

  it("requests the drill-down endpoint with the tenant id in the path", async () => {
    mockedApi.mockResolvedValueOnce(tenantSample);

    const { result } = renderHook(
      () => useDatabaseSizeByTenant("tenant-1", { from: null, to: null }),
      { wrapper: makeWrapper() }
    );

    await waitFor(() => expect(result.current.isSuccess).toBe(true));
    expect(mockedApi).toHaveBeenCalledWith(
      "/api/v1/reports/database-size/tenant-1",
      expect.objectContaining({ schema: expect.anything() })
    );
  });

  it("stays disabled (no request) when no tenant is selected", async () => {
    const { result } = renderHook(() => useDatabaseSizeByTenant(null, { from: null, to: null }), {
      wrapper: makeWrapper(),
    });

    await waitFor(() => expect(result.current.fetchStatus).toBe("idle"));
    expect(mockedApi).not.toHaveBeenCalled();
    expect(result.current.data).toBeUndefined();
  });
});
