import { describe, it, expect, vi, beforeEach } from "vitest";
import { render, screen } from "@testing-library/react";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { MemoryRouter } from "react-router";
import type { ReactNode } from "react";
import "@/i18n";
import type * as UseInfobasesModule from "../useInfobases";

// Тяжёлые дочерние диалоги/карточки заглушаем — тест про набор вкладок, не про них.
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

function renderPage() {
  const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  const wrapper = ({ children }: { children: ReactNode }) => (
    <QueryClientProvider client={client}>
      <MemoryRouter>{children}</MemoryRouter>
    </QueryClientProvider>
  );
  render(<InfobasesPage />, { wrapper });
}

// MLC-215: дом IIS = «Сервер». Вкладка «IIS» удалена из «Баз» — остались «Базы»/«Размер баз».
describe("InfobasesPage — набор вкладок (MLC-215)", () => {
  beforeEach(() => {
    mockedMe.mockReturnValue({ data: { id: "u1", userName: "admin", roles: ["Admin"] } } as never);
    mockedTenants.mockReturnValue({ data: { items: [{ id: "t1", name: "Клиент A" }] } } as never);
    mockedInfobases.mockReturnValue({
      data: { items: [], total: 0, page: 1, pageSize: 25 },
      isLoading: false,
      isError: false,
      isFetching: false,
      refetch: vi.fn(),
    } as never);
    mockedUnassigned.mockReturnValue({
      data: { items: [], hiddenItems: [], available: false, error: null, checkedAtUtc: null },
      isLoading: false,
      isFetching: false,
      refresh: vi.fn(),
    } as never);
  });

  it("показывает вкладки «Базы» и «Размер баз», без вкладки «IIS»", () => {
    renderPage();
    expect(screen.getByRole("tab", { name: "Базы" })).toBeInTheDocument();
    expect(screen.getByRole("tab", { name: "Размер баз" })).toBeInTheDocument();
    expect(screen.queryByRole("tab", { name: "IIS" })).not.toBeInTheDocument();
  });
});
