import { describe, it, expect, vi, beforeEach } from "vitest";
import { render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import type { ReactNode } from "react";
import "@/i18n";
import type * as ApiModule from "@/lib/api";
import { DeleteTenantDialog } from "../DeleteTenantDialog";
import type { Tenant } from "../types";
import { tenantsQueryKey } from "../useTenants";

vi.mock("sonner", () => ({
  toast: { success: vi.fn(), error: vi.fn() },
}));

vi.mock("@/lib/api", async (importOriginal) => {
  const actual = await importOriginal<typeof ApiModule>();
  return { ...actual, api: vi.fn() };
});

import { api, ApiError } from "@/lib/api";
import { toast } from "sonner";

const mockedApi = vi.mocked(api);
const mockedToastSuccess = vi.mocked(toast.success);
const mockedToastError = vi.mocked(toast.error);

const sampleTenant: Tenant = {
  id: "11111111-1111-1111-1111-111111111111",
  name: "Acme",
  maxConcurrentLicenses: 10,
  isActive: true,
  createdAt: "2026-01-01T00:00:00Z",
  updatedAt: null,
  infobaseCount: 0,
  rowVersion: null,
};

function setup() {
  const client = new QueryClient({
    defaultOptions: { queries: { retry: false }, mutations: { retry: false } },
  });
  const invalidateSpy = vi.spyOn(client, "invalidateQueries");
  const onOpenChange = vi.fn();
  const wrapper = ({ children }: { children: ReactNode }) => (
    <QueryClientProvider client={client}>{children}</QueryClientProvider>
  );
  render(<DeleteTenantDialog open onOpenChange={onOpenChange} tenant={sampleTenant} />, {
    wrapper,
  });
  return { invalidateSpy, onOpenChange, user: userEvent.setup() };
}

describe("DeleteTenantDialog", () => {
  beforeEach(() => {
    mockedApi.mockReset();
  });

  it("удаляет клиента (DELETE), инвалидирует кэш и закрывает диалог", async () => {
    mockedApi.mockResolvedValueOnce(null);
    const { invalidateSpy, onOpenChange, user } = setup();

    // Кнопка удаления активируется только при точном совпадении имени.
    await user.type(screen.getByRole("textbox"), sampleTenant.name);
    await user.click(screen.getByRole("button", { name: "Удалить" }));

    await waitFor(() =>
      expect(mockedApi).toHaveBeenCalledWith(`/api/v1/tenants/${sampleTenant.id}`, {
        method: "DELETE",
      })
    );
    expect(invalidateSpy).toHaveBeenCalledWith({ queryKey: tenantsQueryKey });
    expect(mockedToastSuccess).toHaveBeenCalled();
    expect(onOpenChange).toHaveBeenCalledWith(false);
  });

  it("409 TENANT_HAS_INFOBASES → локализованный toast, диалог не закрывается", async () => {
    mockedApi.mockRejectedValueOnce(
      new ApiError(409, "conflict", { code: "TENANT_HAS_INFOBASES" })
    );
    const { onOpenChange, user } = setup();

    await user.type(screen.getByRole("textbox"), sampleTenant.name);
    await user.click(screen.getByRole("button", { name: "Удалить" }));

    await waitFor(() =>
      expect(mockedToastError).toHaveBeenCalledWith("У клиента есть инфобазы. Сначала удалите их.")
    );
    expect(onOpenChange).not.toHaveBeenCalled();
  });
});
