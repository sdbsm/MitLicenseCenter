import { describe, it, expect, vi, beforeEach } from "vitest";
import { render, screen } from "@testing-library/react";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { MemoryRouter } from "react-router";
import type { ReactNode } from "react";
import "@/i18n";
import type * as UseInfobasesModule from "../useInfobases";

// Тяжёлые дочерние диалоги/карточки заглушаем — тест про гейтинг баннера, не про них.
vi.mock("@/features/infobases/InfobaseFormDialog", () => ({ InfobaseFormDialog: () => null }));
vi.mock("@/features/infobases/unassigned/UnassignedInfobasesDialog", () => ({
  UnassignedInfobasesDialog: () => null,
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

function setRoles(roles: string[]) {
  mockedMe.mockReturnValue({ data: { id: "u1", userName: "admin", roles } } as never);
}

function setUnassigned(over: Partial<{ available: boolean; itemCount: number }>) {
  const available = over.available ?? true;
  const itemCount = over.itemCount ?? 0;
  const items = Array.from({ length: itemCount }, (_, i) => ({
    clusterInfobaseId: `0000000${i}-0000-0000-0000-000000000000`,
    name: `bd${i}`,
    description: null,
  }));
  mockedUnassigned.mockReturnValue({
    data: { items, hiddenItems: [], available, error: null, checkedAtUtc: "2026-06-11T10:00:00Z" },
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

const BANNER = /не завед/;

describe("InfobasesPage — гейтинг баннера нераспределённых", () => {
  beforeEach(() => {
    mockedTenants.mockReturnValue({
      data: { items: [{ id: "t1", name: "Клиент A" }] },
    } as never);
    mockedInfobases.mockReturnValue({
      data: { items: [], total: 0, page: 1, pageSize: 25 },
      isLoading: false,
      isError: false,
      isFetching: false,
      refetch: vi.fn(),
    } as never);
  });

  it("админ + available + count>0 → баннер показан", () => {
    setRoles(["Admin"]);
    setUnassigned({ available: true, itemCount: 2 });
    renderPage();
    expect(screen.getByText(BANNER)).toBeInTheDocument();
  });

  it("count=0 → баннер скрыт (ложного нуля не показываем)", () => {
    setRoles(["Admin"]);
    setUnassigned({ available: true, itemCount: 0 });
    renderPage();
    expect(screen.queryByText(BANNER)).not.toBeInTheDocument();
  });

  it("available:false → баннер скрыт", () => {
    setRoles(["Admin"]);
    setUnassigned({ available: false, itemCount: 0 });
    renderPage();
    expect(screen.queryByText(BANNER)).not.toBeInTheDocument();
  });

  it("Viewer → баннер скрыт", () => {
    setRoles(["Viewer"]);
    setUnassigned({ available: true, itemCount: 2 });
    renderPage();
    expect(screen.queryByText(BANNER)).not.toBeInTheDocument();
  });
});
