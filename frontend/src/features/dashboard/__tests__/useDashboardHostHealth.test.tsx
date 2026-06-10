import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { renderHook, waitFor } from "@testing-library/react";
import type { ReactNode } from "react";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { hostMetricsSnapshotSchema } from "@/features/performance/types";
import { DASHBOARD_HOST_REFETCH_MS, useDashboardHostHealth } from "../useDashboardHostHealth";

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

describe("useDashboardHostHealth", () => {
  beforeEach(() => mockedApi.mockReset());

  it("polls every 30–60s, not the 5s live cadence (MLC-085 user decision)", () => {
    expect(DASHBOARD_HOST_REFETCH_MS).toBeGreaterThanOrEqual(30_000);
    expect(DASHBOARD_HOST_REFETCH_MS).toBeLessThanOrEqual(60_000);
  });

  it("requests the host endpoint with the runtime schema (MLC-016 boundary)", async () => {
    mockedApi.mockResolvedValueOnce({});

    const { result } = renderHook(() => useDashboardHostHealth(), { wrapper: makeWrapper() });

    await waitFor(() => expect(result.current.isSuccess).toBe(true));
    expect(mockedApi).toHaveBeenCalledWith(
      "/api/v1/performance/host",
      expect.objectContaining({ schema: hostMetricsSnapshotSchema })
    );
  });
});

// Схема-тест на wire-ответах (ограничение трека, урок MLC-067/071): бэкенд опускает
// null-поля (WhenWritingNull). В HostMetricsSnapshot все поля — value-типы и
// присутствуют всегда; тесты фиксируют это допущение на реальных формах ответа —
// появись в DTO nullable-поле, упавший parse потребует omittable() в схеме.
describe("hostMetricsSnapshotSchema on wire payloads (dashboard consumer)", () => {
  it("parses the first probe (measuring=true, delta metrics zeroed, no process groups)", () => {
    const firstProbe = JSON.parse(
      `{
        "capturedAtUtc": "2026-06-10T08:00:00Z",
        "measuring": true,
        "cpu": { "totalPercent": 0, "queueLength": 0 },
        "memory": { "availableMBytes": 8192, "totalMBytes": 16384, "pagesPerSec": 0 },
        "disk": { "avgReadSecPerOp": 0, "avgWriteSecPerOp": 0, "queueLength": 0 },
        "processGroups": [],
        "processesInaccessible": 0,
        "attributionIncomplete": false
      }`
    ) as unknown;

    const parsed = hostMetricsSnapshotSchema.parse(firstProbe);
    expect(parsed.measuring).toBe(true);
    expect(parsed.processGroups).toEqual([]);
  });

  it("parses a warmed-up snapshot with process groups", () => {
    const snapshot = JSON.parse(
      `{
        "capturedAtUtc": "2026-06-10T08:01:00Z",
        "measuring": false,
        "cpu": { "totalPercent": 37.5, "queueLength": 1 },
        "memory": { "availableMBytes": 4096, "totalMBytes": 16384, "pagesPerSec": 12 },
        "disk": { "avgReadSecPerOp": 0.004, "avgWriteSecPerOp": 0.006, "queueLength": 0.2 },
        "processGroups": [
          { "family": "OneC", "cpuPercent": 21.2, "ramBytes": 2147483648, "processCount": 4 }
        ],
        "processesInaccessible": 2,
        "attributionIncomplete": true
      }`
    ) as unknown;

    const parsed = hostMetricsSnapshotSchema.parse(snapshot);
    expect(parsed.measuring).toBe(false);
    expect(parsed.attributionIncomplete).toBe(true);
  });
});
