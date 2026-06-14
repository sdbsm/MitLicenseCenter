/**
 * MLC-150 — серверный фильтр «не найдена в кластере».
 *
 * 1) useInfobases прокидывает notInCluster=true в query-строку запроса (как publishStatus);
 * 2) infobaseListResponseSchema принимает clusterAvailable (omit/false/true) — parity BE↔FE
 *    для опускаемого null-поля (BE отдаёт флаг только при notInCluster=true).
 */
import { describe, it, expect, vi, beforeEach } from "vitest";
import { renderHook, waitFor } from "@testing-library/react";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import type { ReactNode } from "react";
import type * as ApiModule from "@/lib/api";
import { useInfobases } from "../useInfobases";
import { infobaseListResponseSchema } from "../types";

vi.mock("@/lib/api", async (importOriginal) => {
  const actual = await importOriginal<typeof ApiModule>();
  return { ...actual, api: vi.fn() };
});

import { api } from "@/lib/api";

const mockedApi = vi.mocked(api);

function makeWrapper() {
  const client = new QueryClient({
    defaultOptions: { queries: { retry: false } },
  });
  const wrapper = ({ children }: { children: ReactNode }) => (
    <QueryClientProvider client={client}>{children}</QueryClientProvider>
  );
  return wrapper;
}

function lastUrl(): string {
  const call = mockedApi.mock.calls.at(-1);
  return (call?.[0] as string) ?? "";
}

describe("useInfobases — фильтр notInCluster (MLC-150)", () => {
  beforeEach(() => {
    mockedApi.mockReset();
    mockedApi.mockResolvedValue({ items: [], total: 0, page: 1, pageSize: 25 });
  });

  it("notInCluster=true добавляет ?notInCluster=true в запрос", async () => {
    renderHook(() => useInfobases(null, null, true), { wrapper: makeWrapper() });
    await waitFor(() => expect(mockedApi).toHaveBeenCalled());
    expect(lastUrl()).toContain("notInCluster=true");
  });

  it("notInCluster=false (по умолчанию) не добавляет параметр", async () => {
    renderHook(() => useInfobases(null, null, false), { wrapper: makeWrapper() });
    await waitFor(() => expect(mockedApi).toHaveBeenCalled());
    expect(lastUrl()).not.toContain("notInCluster");
  });
});

describe("infobaseListResponseSchema — clusterAvailable (parity, omit-null)", () => {
  const envelope = { items: [], total: 0, page: 1, pageSize: 25 };

  it("принимает ответ без ключа clusterAvailable (BE опускает null) → null", () => {
    const parsed = infobaseListResponseSchema.parse(envelope);
    expect(parsed.clusterAvailable).toBeNull();
  });

  it("принимает clusterAvailable: false (RAS недоступен при notInCluster)", () => {
    const parsed = infobaseListResponseSchema.parse({ ...envelope, clusterAvailable: false });
    expect(parsed.clusterAvailable).toBe(false);
  });

  it("принимает clusterAvailable: true (снапшот доступен)", () => {
    const parsed = infobaseListResponseSchema.parse({ ...envelope, clusterAvailable: true });
    expect(parsed.clusterAvailable).toBe(true);
  });
});
