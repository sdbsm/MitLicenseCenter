import { describe, it, expect, vi, beforeEach } from "vitest";
import { render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import type { ReactNode } from "react";
import "@/i18n";
import type * as ApiModule from "@/lib/api";
import { TenantFormDialog } from "../TenantFormDialog";
import type { Tenant } from "../types";
import { tenantsQueryKey } from "../useTenants";

vi.mock("sonner", () => ({
  toast: { success: vi.fn(), error: vi.fn() },
}));

// Частичный мок: оставляем настоящий ApiError (диалог делает instanceof-проверку),
// подменяем только сетевой `api`.
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

function setup(tenant?: Tenant | null) {
  const client = new QueryClient({
    defaultOptions: { queries: { retry: false }, mutations: { retry: false } },
  });
  const invalidateSpy = vi.spyOn(client, "invalidateQueries");
  const onOpenChange = vi.fn();
  const wrapper = ({ children }: { children: ReactNode }) => (
    <QueryClientProvider client={client}>{children}</QueryClientProvider>
  );
  render(<TenantFormDialog open onOpenChange={onOpenChange} tenant={tenant} />, { wrapper });
  return { invalidateSpy, onOpenChange, user: userEvent.setup() };
}

describe("TenantFormDialog", () => {
  beforeEach(() => {
    mockedApi.mockReset();
  });

  it("создаёт клиента, инвалидирует кэш и закрывает диалог", async () => {
    mockedApi.mockResolvedValueOnce(sampleTenant);
    const { invalidateSpy, onOpenChange, user } = setup();

    await user.type(screen.getByRole("textbox"), "Acme");
    await user.click(screen.getByRole("button", { name: "Создать" }));

    await waitFor(() =>
      expect(mockedApi).toHaveBeenCalledWith("/api/v1/tenants", {
        method: "POST",
        body: { name: "Acme", maxConcurrentLicenses: 0, isActive: true },
      })
    );
    expect(invalidateSpy).toHaveBeenCalledWith({ queryKey: tenantsQueryKey });
    expect(mockedToastSuccess).toHaveBeenCalled();
    expect(onOpenChange).toHaveBeenCalledWith(false);
  });

  it("обновляет клиента через PUT и инвалидирует кэш", async () => {
    mockedApi.mockResolvedValueOnce({ ...sampleTenant, name: "Acme 2" });
    const { invalidateSpy, onOpenChange, user } = setup(sampleTenant);

    const nameInput = screen.getByRole("textbox");
    await user.clear(nameInput);
    await user.type(nameInput, "Acme 2");
    await user.click(screen.getByRole("button", { name: "Сохранить" }));

    await waitFor(() =>
      expect(mockedApi).toHaveBeenCalledWith(`/api/v1/tenants/${sampleTenant.id}`, {
        method: "PUT",
        body: { name: "Acme 2", maxConcurrentLicenses: 10, isActive: true },
      })
    );
    expect(invalidateSpy).toHaveBeenCalledWith({ queryKey: tenantsQueryKey });
    expect(onOpenChange).toHaveBeenCalledWith(false);
  });

  it("MLC-136 — в режиме редактирования шлёт прочитанный rowVersion в теле PUT", async () => {
    const withToken: Tenant = { ...sampleTenant, rowVersion: "AAAAAAAAB9E=" };
    mockedApi.mockResolvedValueOnce({ ...withToken, name: "Acme 2" });
    const { user } = setup(withToken);

    const nameInput = screen.getByRole("textbox");
    await user.clear(nameInput);
    await user.type(nameInput, "Acme 2");
    await user.click(screen.getByRole("button", { name: "Сохранить" }));

    await waitFor(() =>
      expect(mockedApi).toHaveBeenCalledWith(`/api/v1/tenants/${withToken.id}`, {
        method: "PUT",
        body: {
          name: "Acme 2",
          maxConcurrentLicenses: 10,
          isActive: true,
          rowVersion: "AAAAAAAAB9E=",
        },
      })
    );
  });

  it("MLC-136 — 409 TENANT_CONCURRENCY_CONFLICT → тост, диалог открыт, не ошибка поля", async () => {
    mockedApi.mockRejectedValueOnce(
      new ApiError(409, "conflict", { code: "TENANT_CONCURRENCY_CONFLICT" })
    );
    const withToken: Tenant = { ...sampleTenant, rowVersion: "AAAAAAAAB9E=" };
    const { onOpenChange, user } = setup(withToken);

    const nameInput = screen.getByRole("textbox");
    await user.clear(nameInput);
    await user.type(nameInput, "Acme 2");
    await user.click(screen.getByRole("button", { name: "Сохранить" }));

    await waitFor(() =>
      expect(mockedToastError).toHaveBeenCalledWith(
        "Данные клиента изменены другим пользователем. Обновите страницу и повторите."
      )
    );
    expect(onOpenChange).not.toHaveBeenCalled();
  });

  it("409 NAME_DUPLICATE → локализованная ошибка на поле «Название», диалог открыт", async () => {
    mockedApi.mockRejectedValueOnce(new ApiError(409, "conflict", { code: "NAME_DUPLICATE" }));
    const { onOpenChange, user } = setup();

    await user.type(screen.getByRole("textbox"), "Acme");
    await user.click(screen.getByRole("button", { name: "Создать" }));

    expect(await screen.findByText("Клиент с таким названием уже существует.")).toBeInTheDocument();
    expect(onOpenChange).not.toHaveBeenCalled();
    expect(mockedToastError).not.toHaveBeenCalled();
  });
});
