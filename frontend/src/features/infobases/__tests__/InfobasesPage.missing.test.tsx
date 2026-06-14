import { describe, it, expect, vi, beforeEach } from "vitest";
import { render, screen } from "@testing-library/react";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { MemoryRouter } from "react-router";
import type { ReactNode } from "react";
import "@/i18n";
import type * as UseInfobasesModule from "../useInfobases";

// Тяжёлые дочерние диалоги заглушаем — тест про гейтинг баннера обратного дрейфа и метку
// на строке, не про диалоги. Таблицу (DataTable + infobaseColumns) НЕ мокаем —
// проверяем рендер метки по membership.
vi.mock("@/features/infobases/InfobaseFormDialog", () => ({ InfobaseFormDialog: () => null }));
vi.mock("@/features/infobases/unassigned/UnassignedInfobasesDialog", () => ({
  UnassignedInfobasesDialog: () => null,
}));
vi.mock("@/features/infobases/unassigned/MissingInfobasesDialog", () => ({
  MissingInfobasesDialog: () => null,
}));
vi.mock("@/features/infobases/DeleteInfobaseDialog", () => ({ DeleteInfobaseDialog: () => null }));
vi.mock("@/features/infobases/ReassignInfobaseDialog", () => ({
  ReassignInfobaseDialog: () => null,
}));
vi.mock("@/features/backups/BackupsDialog", () => ({ BackupsDialog: () => null }));
vi.mock("@/features/publications/PublishPublicationDialog", () => ({
  PublishPublicationDialog: () => null,
}));
vi.mock("@/features/publications/UnpublishPublicationDialog", () => ({
  UnpublishPublicationDialog: () => null,
}));
vi.mock("@/features/publications/ChangePlatformDialog", () => ({
  ChangePlatformDialog: () => null,
}));
vi.mock("@/features/publications/BulkPublishDialog", () => ({ BulkPublishDialog: () => null }));
vi.mock("@/features/publications/BulkChangePlatformDialog", () => ({
  BulkChangePlatformDialog: () => null,
}));

vi.mock("@/features/auth/useAuth", () => ({ useMe: vi.fn() }));
vi.mock("@/features/tenants/useTenants", () => ({ useAllTenants: vi.fn() }));
vi.mock("@/features/publications/usePublications", () => ({
  useCheckStatus: () => ({ mutateAsync: vi.fn(), isPending: false }),
}));
vi.mock("@/features/infobases/useInfobases", async (importOriginal) => {
  const actual = await importOriginal<typeof UseInfobasesModule>();
  return { ...actual, useInfobases: vi.fn() };
});
vi.mock("@/features/infobases/unassigned/useUnassignedInfobases", () => ({
  useUnassignedInfobases: vi.fn(),
}));

import { useMe } from "@/features/auth/useAuth";
import { useAllTenants } from "@/features/tenants/useTenants";
import { useInfobases } from "../useInfobases";
import { useUnassignedInfobases } from "../unassigned/useUnassignedInfobases";
import { InfobasesPage } from "../InfobasesPage";

const mockedMe = vi.mocked(useMe);
const mockedTenants = vi.mocked(useAllTenants);
const mockedInfobases = vi.mocked(useInfobases);
const mockedUnassigned = vi.mocked(useUnassignedInfobases);

const GHOST_UUID = "11111111-1111-1111-1111-111111111111";

function row() {
  return {
    id: "ib-1",
    tenantId: "t1",
    name: "Призрак",
    clusterInfobaseId: GHOST_UUID,
    databaseName: "ghost",
    status: "Active",
    createdAt: "2026-01-01T00:00:00Z",
    updatedAt: null,
    tenantName: "Клиент A",
    publication: {
      id: "p1",
      infobaseId: "ib-1",
      siteName: "Default Web Site",
      virtualPath: "/ghost",
      platformVersion: "8.3.23.1865",
      source: "Webinst",
      physicalPathOverride: null,
      createdAt: "2026-01-01T00:00:00Z",
      updatedAt: null,
      lastCheckStatus: "Published",
      lastCheckAt: null,
      lastCheckDetails: null,
    },
  };
}

function setRoles(roles: string[]) {
  mockedMe.mockReturnValue({ data: { id: "u1", userName: "admin", roles } } as never);
}

function setUnassigned(over: Partial<{ available: boolean; missing: boolean }>) {
  const available = over.available ?? true;
  const missingItems = over.missing
    ? [
        {
          infobaseId: "ib-1",
          tenantName: "Клиент A",
          name: "Призрак",
          clusterInfobaseId: GHOST_UUID,
        },
      ]
    : [];
  mockedUnassigned.mockReturnValue({
    data: {
      items: [],
      hiddenItems: [],
      missingItems,
      available,
      error: null,
      checkedAtUtc: "2026-06-11T10:00:00Z",
    },
    isLoading: false,
    isFetching: false,
    refresh: vi.fn(),
  } as never);
}

function renderPage() {
  const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  const wrapper = ({ children }: { children: ReactNode }) => (
    <QueryClientProvider client={client}>
      <MemoryRouter>{children}</MemoryRouter>
    </QueryClientProvider>
  );
  render(<InfobasesPage />, { wrapper });
}

const BANNER = /в кластере 1С/;
const LABEL = "Не найдена в кластере";

describe("InfobasesPage — обратный дрейф (баннер + метка)", () => {
  beforeEach(() => {
    mockedTenants.mockReturnValue({ data: { items: [{ id: "t1", name: "Клиент A" }] } } as never);
    mockedInfobases.mockReturnValue({
      data: { items: [row()], total: 1, page: 1, pageSize: 25 },
      isLoading: false,
      isError: false,
      isFetching: false,
      refetch: vi.fn(),
    } as never);
  });

  it("админ + available + missingItems → красный баннер и метка на строке", () => {
    setRoles(["Admin"]);
    setUnassigned({ available: true, missing: true });
    renderPage();
    expect(screen.getByText(BANNER)).toBeInTheDocument();
    expect(screen.getByText(LABEL)).toBeInTheDocument();
  });

  it("пустой missingItems → ни баннера, ни метки", () => {
    setRoles(["Admin"]);
    setUnassigned({ available: true, missing: false });
    renderPage();
    expect(screen.queryByText(BANNER)).not.toBeInTheDocument();
    expect(screen.queryByText(LABEL)).not.toBeInTheDocument();
  });

  it("available:false → ни баннера, ни метки (сбой опроса ≠ пропавшие базы)", () => {
    setRoles(["Admin"]);
    setUnassigned({ available: false, missing: true });
    renderPage();
    expect(screen.queryByText(BANNER)).not.toBeInTheDocument();
    expect(screen.queryByText(LABEL)).not.toBeInTheDocument();
  });

  it("Viewer → ни баннера, ни метки", () => {
    setRoles(["Viewer"]);
    setUnassigned({ available: true, missing: true });
    renderPage();
    expect(screen.queryByText(BANNER)).not.toBeInTheDocument();
    expect(screen.queryByText(LABEL)).not.toBeInTheDocument();
  });
});
