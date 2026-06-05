import { describe, it, expect, vi, beforeEach } from "vitest";
import { renderHook, act, waitFor } from "@testing-library/react";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import type { ReactNode } from "react";
import "@/i18n";
import type * as ApiModule from "@/lib/api";
import { useInfobaseForm } from "../useInfobaseForm";
import type { InfobaseListItem } from "../types";
import type { Tenant } from "@/features/tenants/types";

// Замокан только `api`; ApiError/readConflictBody остаются реальными (нужны
// mapConflictToField). GET-настроек резолвятся заданным каталогом, discovery — пустыми,
// точечная занятость — свободно. Поведение хука проверяется без рендера диалога.
vi.mock("@/lib/api", async (importOriginal) => {
  const actual = await importOriginal<typeof ApiModule>();
  return { ...actual, api: vi.fn() };
});

import { api } from "@/lib/api";

const mockedApi = vi.mocked(api);

const tenant: Tenant = {
  id: "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa",
  name: "Клиент A",
  maxConcurrentLicenses: 10,
  isActive: true,
  createdAt: "2026-01-01T00:00:00Z",
  updatedAt: null,
  infobaseCount: 1,
};

const infobase = {
  id: "cccccccc-cccc-cccc-cccc-cccccccccccc",
  tenantId: tenant.id,
  name: "База 1",
  clusterInfobaseId: "dddddddd-dddd-dddd-dddd-dddddddddddd",
  databaseServer: "sql-01",
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

const settingsCatalog = [
  { key: "Defaults.DatabaseServer", value: "sql-default" },
  { key: "OneC.DefaultPlatformVersion", value: "8.3.99.1" },
  { key: "IIS.DefaultSiteName", value: "My Site" },
];

function mockApi() {
  mockedApi.mockImplementation((path: string) => {
    if (path === "/api/v1/settings") return Promise.resolve(settingsCatalog);
    if (path.startsWith("/api/v1/infobases/cluster-id-availability")) {
      return Promise.resolve({ taken: false, takenByTenantName: null });
    }
    return Promise.resolve({ items: [], available: true, error: null });
  });
}

function renderForm(infobaseArg: InfobaseListItem | null) {
  const client = new QueryClient({
    defaultOptions: { queries: { retry: false }, mutations: { retry: false } },
  });
  const onOpenChange = vi.fn();
  const wrapper = ({ children }: { children: ReactNode }) => (
    <QueryClientProvider client={client}>{children}</QueryClientProvider>
  );
  const view = renderHook(
    () => useInfobaseForm({ open: true, onOpenChange, infobase: infobaseArg, tenants: [tenant] }),
    { wrapper }
  );
  return { ...view, onOpenChange };
}

describe("useInfobaseForm", () => {
  beforeEach(() => {
    mockedApi.mockReset();
    mockApi();
  });

  it("create: prefill настроек подставляется в пустые поля после загрузки каталога", async () => {
    const { result } = renderForm(null);

    expect(result.current.isEdit).toBe(false);
    expect(result.current.pending).toBe(false);
    // На mount'е настроек ещё нет — поле пустое.
    expect(result.current.form.getValues("databaseServer")).toBe("");

    await waitFor(() =>
      expect(result.current.form.getValues("databaseServer")).toBe("sql-default")
    );
    expect(result.current.form.getValues("publication.platformVersion")).toBe("8.3.99.1");
    expect(result.current.form.getValues("publication.siteName")).toBe("My Site");
  });

  it("edit: стартует со значений инфобазы; prefill настроек их не перетирает", async () => {
    const { result } = renderForm(infobase);

    expect(result.current.isEdit).toBe(true);
    expect(result.current.form.getValues("databaseServer")).toBe("sql-01");
    expect(result.current.form.getValues("publication.siteName")).toBe("Default Web Site");

    // Дать настройкам/точечной проверке резолвиться — значения должны остаться прежними.
    await waitFor(() => expect(mockedApi).toHaveBeenCalled());
    await Promise.resolve();
    expect(result.current.form.getValues("databaseServer")).toBe("sql-01");
    expect(result.current.form.getValues("publication.siteName")).toBe("Default Web Site");
  });

  it("create: ввод имени БД заполняет virtualPath и physicalPath, пока не тронуты вручную", () => {
    const { result } = renderForm(null);

    act(() => result.current.handleDatabaseNameChange("acme_bp", () => {}));

    expect(result.current.form.getValues("publication.virtualPath")).toBe("/acme-bp");
    expect(result.current.form.getValues("publication.physicalPathOverride")).toBe(
      "C:\\inetpub\\wwwroot\\acme_bp"
    );
  });

  it("create: после ручной правки virtualPath автоподстановка пути отключается точечно", () => {
    const { result } = renderForm(null);

    act(() => result.current.markVirtualPathTouched());
    act(() => result.current.handleDatabaseNameChange("acme_bp", () => {}));

    // virtualPath тронут — не перетираем; physicalPath не тронут — заполняется.
    expect(result.current.form.getValues("publication.virtualPath")).toBe("");
    expect(result.current.form.getValues("publication.physicalPathOverride")).toBe(
      "C:\\inetpub\\wwwroot\\acme_bp"
    );
  });
});
