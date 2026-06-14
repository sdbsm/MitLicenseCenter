import { describe, it, expect, vi, beforeEach } from "vitest";
import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import type { ReactNode } from "react";
import "@/i18n";
import type * as ApiModule from "@/lib/api";
import { InfobaseFormDialog } from "../InfobaseFormDialog";
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
const mockedToastError = vi.mocked(toast.error);

const tenant: Tenant = {
  id: "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa",
  name: "Клиент A",
  maxConcurrentLicenses: 10,
  isActive: true,
  createdAt: "2026-01-01T00:00:00Z",
  updatedAt: null,
  infobaseCount: 1,
  rowVersion: null,
};

// Полностью валидная инфобаза — в edit-режиме форма стартует с этих значений,
// поэтому zod-валидация проходит и submit доходит до мутации.
const infobase = {
  id: "cccccccc-cccc-cccc-cccc-cccccccccccc",
  tenantId: tenant.id,
  name: "База 1",
  clusterInfobaseId: "dddddddd-dddd-dddd-dddd-dddddddddddd",
  databaseName: "acme",
  status: "Active",
  createdAt: "2026-01-01T00:00:00Z",
  updatedAt: null,
  tenantName: tenant.name,
  publication: {
    id: "eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee",
    infobaseId: "cccccccc-cccc-cccc-cccc-cccccccccccc",
    siteName: "Default Web Site",
    virtualPath: "/acme",
    platformVersion: "8.3.23.1865",
    source: "Unknown",
    physicalPathOverride: null,
    createdAt: "2026-01-01T00:00:00Z",
    updatedAt: null,
    lastCheckStatus: "Unknown",
    lastCheckAt: null,
    lastCheckDetails: null,
  },
} as InfobaseListItem;

// Реальные хуки-мутации/запросы работают поверх замоканного `api`. GET-запросы
// (discovery, список баз, настройки) резолвятся пустыми; PUT инфобазы — отклоняем
// с заданным 409-кодом, чтобы проверить маппинг в поле формы.
function mockApiRejectingPutWith(code: string) {
  mockedApi.mockImplementation((path: string, opts?: { method?: string }) => {
    const method = opts?.method ?? "GET";
    if (method === "PUT" && path.startsWith("/api/v1/infobases/")) {
      return Promise.reject(new ApiError(409, "conflict", { code }));
    }
    if (path === "/api/v1/settings") return Promise.resolve([]);
    return Promise.resolve({ items: [], available: true, error: null });
  });
}

function setup() {
  const client = new QueryClient({
    defaultOptions: { queries: { retry: false }, mutations: { retry: false } },
  });
  const onOpenChange = vi.fn();
  const wrapper = ({ children }: { children: ReactNode }) => (
    <QueryClientProvider client={client}>{children}</QueryClientProvider>
  );
  render(
    <InfobaseFormDialog open onOpenChange={onOpenChange} infobase={infobase} tenants={[tenant]} />,
    { wrapper }
  );
  return { onOpenChange, user: userEvent.setup() };
}

describe("InfobaseFormDialog — маппинг 409", () => {
  beforeEach(() => {
    mockedApi.mockReset();
  });

  it("INFOBASE_ALREADY_ASSIGNED → ошибка на поле «база кластера»", async () => {
    mockApiRejectingPutWith("INFOBASE_ALREADY_ASSIGNED");
    const { onOpenChange, user } = setup();

    await user.click(screen.getByRole("button", { name: "Сохранить" }));

    expect(
      await screen.findByText("Эта база кластера уже привязана к другому клиенту.")
    ).toBeInTheDocument();
    expect(onOpenChange).not.toHaveBeenCalled();
    expect(mockedToastError).not.toHaveBeenCalled();
  });

  it("занятая база кластера (точечная проверка) → именованная ошибка, submit не уходит в сеть", async () => {
    // MLC-015 — форма больше не выгружает все базы: занятость проверяется точечным
    // эндпоинтом. Заняв базу, ждём именованную ошибку и отсутствие PUT.
    mockedApi.mockImplementation((path: string, opts?: { method?: string }) => {
      const method = opts?.method ?? "GET";
      if (path.startsWith("/api/v1/infobases/cluster-id-availability")) {
        return Promise.resolve({ taken: true, takenByTenantName: "Клиент B" });
      }
      if (path === "/api/v1/settings") return Promise.resolve([]);
      if (method === "PUT") return Promise.reject(new Error("PUT не должен вызываться"));
      return Promise.resolve({ items: [], available: true, error: null });
    });
    const { onOpenChange, user } = setup();

    expect(
      await screen.findByText("Эта база кластера уже привязана к клиенту «Клиент B».")
    ).toBeInTheDocument();

    await user.click(screen.getByRole("button", { name: "Сохранить" }));

    expect(onOpenChange).not.toHaveBeenCalled();
    expect(mockedToastError).not.toHaveBeenCalled();
  });

  it("NAME_DUPLICATE_IN_TENANT → раскрывает «Дополнительно» и показывает ошибку на поле имени", async () => {
    mockApiRejectingPutWith("NAME_DUPLICATE_IN_TENANT");
    const { onOpenChange, user } = setup();

    await user.click(screen.getByRole("button", { name: "Сохранить" }));

    expect(
      await screen.findByText("У этого клиента уже есть инфобаза с таким названием.")
    ).toBeInTheDocument();
    expect(onOpenChange).not.toHaveBeenCalled();
  });
});
