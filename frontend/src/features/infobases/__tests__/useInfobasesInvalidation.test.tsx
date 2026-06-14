/**
 * FE-02: проверка инвалидации кэша клиентов (tenantsQueryKey) при create и delete.
 *
 * Счётчик баз клиента на странице /tenants берётся из кэша tenants, поэтому
 * useCreateInfobase и useDeleteInfobase должны инвалидировать ОБА ключа:
 * infobasesQueryKey + tenantsQueryKey. Образец — useReassignInfobase, уже покрытый
 * ReassignInfobaseDialog.test.tsx.
 */
import { describe, it, expect, vi, beforeEach } from "vitest";
import { renderHook, act, waitFor } from "@testing-library/react";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import type { ReactNode } from "react";
import type * as ApiModule from "@/lib/api";
import { useCreateInfobase, useDeleteInfobase, infobasesQueryKey } from "../useInfobases";
import { tenantsQueryKey } from "@/features/tenants/useTenants";

vi.mock("@/lib/api", async (importOriginal) => {
  const actual = await importOriginal<typeof ApiModule>();
  return { ...actual, api: vi.fn() };
});

import { api } from "@/lib/api";

const mockedApi = vi.mocked(api);

function makeWrapper() {
  const client = new QueryClient({
    defaultOptions: { queries: { retry: false }, mutations: { retry: false } },
  });
  const invalidateSpy = vi.spyOn(client, "invalidateQueries");
  const wrapper = ({ children }: { children: ReactNode }) => (
    <QueryClientProvider client={client}>{children}</QueryClientProvider>
  );
  return { client, invalidateSpy, wrapper };
}

describe("useCreateInfobase / useDeleteInfobase — инвалидация кэша клиентов (FE-02)", () => {
  beforeEach(() => {
    mockedApi.mockReset();
  });

  it("useCreateInfobase: после успешного создания инвалидирует infobases и tenants", async () => {
    mockedApi.mockResolvedValueOnce({
      id: "new-id",
      tenantId: "t1",
      name: "База",
      clusterInfobaseId: "guid-1",
      databaseName: "db",
      status: "Active",
      createdAt: "2026-01-01T00:00:00Z",
      updatedAt: null,
      tenantName: "Клиент",
      publication: null,
    });
    const { invalidateSpy, wrapper } = makeWrapper();
    const { result } = renderHook(() => useCreateInfobase(), { wrapper });

    await act(() =>
      result.current.mutateAsync({
        tenantId: "t1",
        name: "База",
        clusterInfobaseId: "guid-1",
        databaseName: "db",
        status: "Active",
        publication: {
          siteName: "Default Web Site",
          virtualPath: "/db",
          platformVersion: "8.3.23.1865",
          physicalPathOverride: null,
        },
      })
    );

    await waitFor(() =>
      expect(invalidateSpy).toHaveBeenCalledWith({ queryKey: infobasesQueryKey })
    );
    expect(invalidateSpy).toHaveBeenCalledWith({ queryKey: tenantsQueryKey });
  });

  it("useDeleteInfobase: после успешного удаления инвалидирует infobases и tenants", async () => {
    mockedApi.mockResolvedValueOnce(null);
    const { invalidateSpy, wrapper } = makeWrapper();
    const { result } = renderHook(() => useDeleteInfobase(), { wrapper });

    await act(() => result.current.mutateAsync({ id: "ib-1", unpublishFromIis: false }));

    await waitFor(() =>
      expect(invalidateSpy).toHaveBeenCalledWith({ queryKey: infobasesQueryKey })
    );
    expect(invalidateSpy).toHaveBeenCalledWith({ queryKey: tenantsQueryKey });
  });
});
