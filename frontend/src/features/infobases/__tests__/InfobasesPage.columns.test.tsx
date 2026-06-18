import { describe, it, expect, vi, beforeEach } from "vitest";
import { render, screen } from "@testing-library/react";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { MemoryRouter } from "react-router";
import type { ReactNode } from "react";
import "@/i18n";
import type * as UseInfobasesModule from "../useInfobases";

// MLC-206 (§1.4): «Версия платформы» и «Проверено» по умолчанию скрыты — целевой набор
// колонок «Баз» = ☐·База·Клиент·Статус·Публикация·Размер·⋯. Таблицу не мокаем —
// проверяем реальную видимость колонок DataTable; дочерние диалоги заглушены.
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

function row() {
  return {
    id: "ib-1",
    tenantId: "t1",
    name: "База А",
    clusterInfobaseId: "11111111-1111-1111-1111-111111111111",
    databaseName: "base_a",
    status: "Active",
    createdAt: "2026-01-01T00:00:00Z",
    updatedAt: null,
    tenantName: "Клиент A",
    currentDataBytes: null,
    currentLogBytes: null,
    publication: {
      id: "p1",
      infobaseId: "ib-1",
      siteName: "Default Web Site",
      virtualPath: "/base_a",
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

function renderPage() {
  const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  const wrapper = ({ children }: { children: ReactNode }) => (
    <QueryClientProvider client={client}>
      <MemoryRouter>{children}</MemoryRouter>
    </QueryClientProvider>
  );
  render(<InfobasesPage />, { wrapper });
}

describe("InfobasesPage — видимость колонок по умолчанию (MLC-206)", () => {
  beforeEach(() => {
    mockedMe.mockReturnValue({ data: { id: "u1", userName: "admin", roles: ["Admin"] } } as never);
    mockedTenants.mockReturnValue({ data: { items: [{ id: "t1", name: "Клиент A" }] } } as never);
    mockedInfobases.mockReturnValue({
      data: { items: [row()], total: 1, page: 1, pageSize: 25 },
      isLoading: false,
      isError: false,
      isFetching: false,
      refetch: vi.fn(),
    } as never);
    mockedUnassigned.mockReturnValue({
      data: {
        items: [],
        hiddenItems: [],
        missingItems: [],
        available: false,
        error: null,
        checkedAtUtc: null,
      },
      isLoading: false,
      isFetching: false,
      refresh: vi.fn(),
    } as never);
  });

  it("целевой набор колонок виден, «Версия платформы» и «Проверено» скрыты", () => {
    renderPage();

    // Видны по умолчанию.
    expect(screen.getByRole("columnheader", { name: "Название" })).toBeInTheDocument();
    expect(screen.getByRole("columnheader", { name: "Клиент" })).toBeInTheDocument();
    expect(screen.getByRole("columnheader", { name: "Статус базы" })).toBeInTheDocument();
    expect(screen.getByRole("columnheader", { name: "Публикация" })).toBeInTheDocument();
    expect(screen.getByRole("columnheader", { name: "Размер" })).toBeInTheDocument();

    // Скрыты по умолчанию (доступны через меню «Колонки»).
    expect(
      screen.queryByRole("columnheader", { name: "Версия платформы" })
    ).not.toBeInTheDocument();
    expect(screen.queryByRole("columnheader", { name: "Проверено" })).not.toBeInTheDocument();
  });
});
