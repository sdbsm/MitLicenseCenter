import { describe, it, expect, vi, beforeEach } from "vitest";
import { render, screen, waitFor } from "@testing-library/react";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import type { ReactNode } from "react";
import "@/i18n";
import type * as ApiModule from "@/lib/api";
import { InfobaseFormDialog } from "../../InfobaseFormDialog";
import type { Tenant } from "@/features/tenants/types";

vi.mock("sonner", () => ({ toast: { success: vi.fn(), error: vi.fn() } }));

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
  infobaseCount: 0,
  rowVersion: null,
};

const PREFILL = { clusterInfobaseId: "11111111-1111-1111-1111-111111111111", name: "bd1" };

// cluster-infobases отдаём недоступными → поле базы кластера в ручном режиме (Input),
// чтобы напрямую прочитать подставленный GUID. Список БД доступен и содержит совпадение
// по имени (другой регистр) — проверяем best-effort угадывание имени БД.
function mockDiscovery() {
  mockedApi.mockImplementation((path: string) => {
    if (path === "/api/v1/settings") return Promise.resolve([]);
    if (path.startsWith("/api/v1/discovery/cluster-infobases")) {
      return Promise.resolve({ items: [], available: false, error: "ras-down" });
    }
    if (path.startsWith("/api/v1/discovery/databases")) {
      return Promise.resolve({ items: ["BD1"], available: true, error: null });
    }
    if (path.startsWith("/api/v1/infobases/cluster-id-availability")) {
      return Promise.resolve({ taken: false, takenByTenantName: null });
    }
    return Promise.resolve({ items: [], available: true, error: null });
  });
}

function setup() {
  const client = new QueryClient({
    defaultOptions: { queries: { retry: false }, mutations: { retry: false } },
  });
  const wrapper = ({ children }: { children: ReactNode }) => (
    <QueryClientProvider client={client}>{children}</QueryClientProvider>
  );
  render(
    <InfobaseFormDialog
      open
      onOpenChange={vi.fn()}
      infobase={null}
      tenants={[tenant]}
      prefill={PREFILL}
    />,
    { wrapper }
  );
}

describe("InfobaseFormDialog — префилл из «Назначить»", () => {
  beforeEach(() => {
    mockedApi.mockReset();
    mockDiscovery();
  });

  it("подставляет UUID базы кластера", () => {
    setup();
    expect(screen.getByDisplayValue(PREFILL.clusterInfobaseId)).toBeInTheDocument();
  });

  it("угадывает имя БД по совпадению имени (case-insensitive)", async () => {
    setup();
    // Поле «Имя БД» — Radix Select (список доступен); подставленное значение читаем из
    // скрытого native <select> (текст триггера в jsdom при закрытом списке не резолвится).
    await waitFor(() => {
      expect(screen.getByDisplayValue("BD1")).toBeInTheDocument();
    });
  });
});
