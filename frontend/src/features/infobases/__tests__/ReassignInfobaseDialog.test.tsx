import { describe, it, expect, vi, beforeEach } from "vitest";
import { render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import type { ReactNode } from "react";
import "@/i18n";
import type * as ApiModule from "@/lib/api";
import { ReassignInfobaseDialog } from "../ReassignInfobaseDialog";
import { infobasesQueryKey } from "../useInfobases";
import { tenantsQueryKey } from "@/features/tenants/useTenants";
import type { InfobaseListItem } from "../types";
import type { Tenant } from "@/features/tenants/types";

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

const tenantA: Tenant = {
  id: "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa",
  name: "Клиент A",
  maxConcurrentLicenses: 10,
  isActive: true,
  createdAt: "2026-01-01T00:00:00Z",
  updatedAt: null,
  infobaseCount: 1,
};
const tenantB: Tenant = {
  id: "bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb",
  name: "Клиент B",
  maxConcurrentLicenses: 10,
  isActive: true,
  createdAt: "2026-01-01T00:00:00Z",
  updatedAt: null,
  infobaseCount: 0,
};

const infobase = {
  id: "cccccccc-cccc-cccc-cccc-cccccccccccc",
  tenantId: tenantA.id,
  name: "База 1",
  clusterInfobaseId: "dddddddd-dddd-dddd-dddd-dddddddddddd",
  databaseName: "acme",
  status: "Active",
  createdAt: "2026-01-01T00:00:00Z",
  updatedAt: null,
  tenantName: tenantA.name,
} as InfobaseListItem;

function setup() {
  const client = new QueryClient({
    defaultOptions: { queries: { retry: false }, mutations: { retry: false } },
  });
  const invalidateSpy = vi.spyOn(client, "invalidateQueries");
  const onOpenChange = vi.fn();
  const wrapper = ({ children }: { children: ReactNode }) => (
    <QueryClientProvider client={client}>{children}</QueryClientProvider>
  );
  render(
    <ReassignInfobaseDialog
      open
      onOpenChange={onOpenChange}
      infobase={infobase}
      tenants={[tenantA, tenantB]}
    />,
    { wrapper }
  );
  return { invalidateSpy, onOpenChange, user: userEvent.setup() };
}

// Выбирает целевого клиента в Radix Select.
async function pickTarget(user: ReturnType<typeof userEvent.setup>) {
  await user.click(screen.getByRole("combobox"));
  await user.click(await screen.findByRole("option", { name: tenantB.name }));
}

describe("ReassignInfobaseDialog", () => {
  beforeEach(() => {
    mockedApi.mockReset();
  });

  it("переносит инфобазу и инвалидирует кэши infobases и tenants", async () => {
    mockedApi.mockResolvedValueOnce({});
    const { invalidateSpy, onOpenChange, user } = setup();

    await pickTarget(user);
    await user.click(screen.getByRole("button", { name: "Перенести" }));

    await waitFor(() =>
      expect(mockedApi).toHaveBeenCalledWith(
        `/api/v1/infobases/${infobase.id}/reassign`,
        expect.objectContaining({
          method: "POST",
          body: { targetTenantId: tenantB.id },
          schema: expect.anything(),
        })
      )
    );
    expect(invalidateSpy).toHaveBeenCalledWith({ queryKey: infobasesQueryKey });
    expect(invalidateSpy).toHaveBeenCalledWith({ queryKey: tenantsQueryKey });
    expect(mockedToastSuccess).toHaveBeenCalled();
    expect(onOpenChange).toHaveBeenCalledWith(false);
  });

  it("409 INFOBASE_NAME_TAKEN_IN_TARGET → локализованная ошибка в диалоге, без закрытия", async () => {
    mockedApi.mockRejectedValueOnce(
      new ApiError(409, "conflict", { code: "INFOBASE_NAME_TAKEN_IN_TARGET" })
    );
    const { onOpenChange, user } = setup();

    await pickTarget(user);
    await user.click(screen.getByRole("button", { name: "Перенести" }));

    expect(
      await screen.findByText(
        "У целевого клиента уже есть инфобаза с таким названием. Переименуйте базу перед переносом."
      )
    ).toBeInTheDocument();
    expect(onOpenChange).not.toHaveBeenCalled();
    expect(mockedToastError).not.toHaveBeenCalled();
  });
});
