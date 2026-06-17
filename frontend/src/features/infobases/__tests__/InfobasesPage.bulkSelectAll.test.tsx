import { describe, it, expect, vi, beforeEach } from "vitest";
import { render, screen, act, fireEvent, waitFor, within } from "@testing-library/react";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { MemoryRouter } from "react-router";
import type { ReactNode } from "react";
import "@/i18n";
import type * as UseInfobasesModule from "../useInfobases";
import type { InfobaseListItem } from "../types";

// MLC-181c — «Выбрать все N по фильтру»: bulk-бар дёргает /infobases/ids и наполняет тот же
// внешний выбор строками за пределами текущей страницы; capped → тост без выбора; bulk-диалог
// показывает N и сводку активного фильтра.

// Тяжёлые дочерние диалоги/карточки, не относящиеся к bulk-выбору, заглушаем. BulkPublishDialog
// оставляем реальным — проверяем N и сводку фильтра в нём.
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
vi.mock("@/features/publications/BulkChangePlatformDialog", () => ({
  BulkChangePlatformDialog: () => null,
}));

const toastError = vi.fn();
vi.mock("sonner", () => ({
  toast: { success: vi.fn(), error: (...args: unknown[]) => toastError(...args) },
}));

vi.mock("@/features/auth/useAuth", () => ({ useMe: vi.fn() }));
vi.mock("@/features/tenants/useTenants", () => ({ useAllTenants: vi.fn() }));
vi.mock("@/features/publications/usePublications", () => ({
  useCheckStatus: () => ({ mutateAsync: vi.fn(), isPending: false }),
}));
vi.mock("@/features/infobases/useInfobases", async (importOriginal) => {
  const actual = await importOriginal<typeof UseInfobasesModule>();
  return { ...actual, useInfobases: vi.fn(), fetchInfobaseBulkIds: vi.fn() };
});
vi.mock("@/features/infobases/unassigned/useUnassignedInfobases", () => ({
  useUnassignedInfobases: vi.fn(),
}));

import { useMe } from "@/features/auth/useAuth";
import { useAllTenants } from "@/features/tenants/useTenants";
import { useInfobases, fetchInfobaseBulkIds } from "../useInfobases";
import { useUnassignedInfobases } from "../unassigned/useUnassignedInfobases";
import { InfobasesPage } from "../InfobasesPage";

const mockedMe = vi.mocked(useMe);
const mockedTenants = vi.mocked(useAllTenants);
const mockedInfobases = vi.mocked(useInfobases);
const mockedFetchIds = vi.mocked(fetchInfobaseBulkIds);
const mockedUnassigned = vi.mocked(useUnassignedInfobases);

function makeItem(suffix: string): InfobaseListItem {
  return {
    id: `ib-${suffix}`,
    tenantId: "t1",
    tenantName: "Клиент A",
    name: `База ${suffix}`,
    clusterInfobaseId: `cl-${suffix}`,
    databaseName: `db_${suffix}`,
    status: "Active",
    createdAt: "2026-06-01T00:00:00Z",
    updatedAt: null,
    rowVersion: null,
    currentDataBytes: null,
    currentLogBytes: null,
    publication: {
      id: `pub-${suffix}`,
      infobaseId: `ib-${suffix}`,
      siteName: "Default Web Site",
      virtualPath: `/db-${suffix}`,
      platformVersion: "8.3.23.1865",
      source: "Webinst",
      physicalPathOverride: null,
      createdAt: "2026-06-01T00:00:00Z",
      updatedAt: null,
      lastCheckStatus: "Published",
      lastCheckAt: null,
      lastCheckDetails: null,
      rowVersion: null,
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

describe("InfobasesPage — «Выбрать все N по фильтру» (MLC-181c)", () => {
  beforeEach(() => {
    vi.clearAllMocks();
    mockedMe.mockReturnValue({ data: { id: "u1", userName: "admin", roles: ["Admin"] } } as never);
    mockedTenants.mockReturnValue({
      data: { items: [{ id: "t1", name: "Клиент A" }] },
    } as never);
    // На странице одна видимая строка, но total=3 — остальные на других страницах.
    mockedInfobases.mockReturnValue({
      data: { items: [makeItem("page1")], total: 3, page: 1, pageSize: 25 },
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

  function selectVisibleRow() {
    const rowCheckbox = screen.getByRole("checkbox", { name: /Выбрать публикацию/ });
    act(() => {
      fireEvent.click(rowCheckbox);
    });
  }

  it("дёргает /ids и наполняет выбор строками за пределами текущей страницы", async () => {
    mockedFetchIds.mockResolvedValue({
      items: [
        {
          infobaseId: "ib-1",
          publicationId: "pub-1",
          infobaseName: "База 1",
          siteName: "S",
          virtualPath: "/1",
        },
        {
          infobaseId: "ib-2",
          publicationId: "pub-2",
          infobaseName: "База 2",
          siteName: "S",
          virtualPath: "/2",
        },
        {
          infobaseId: "ib-3",
          publicationId: "pub-3",
          infobaseName: "База 3",
          siteName: "S",
          virtualPath: "/3",
        },
      ],
      total: 3,
      capped: false,
    });

    renderPage();
    selectVisibleRow();

    // Бар появился — выбрана 1 (видимая страница).
    expect(screen.getByText(/Выбрано публикаций: 1/)).toBeInTheDocument();

    const selectAll = screen.getByRole("button", { name: "Выбрать все по фильтру" });
    await act(async () => {
      fireEvent.click(selectAll);
    });

    // После наполнения /ids — выбраны все 3 (за пределами видимой страницы).
    await waitFor(() => expect(screen.getByText(/Выбрано публикаций: 3/)).toBeInTheDocument());
    expect(mockedFetchIds).toHaveBeenCalledTimes(1);
  });

  it("capped → тост, выбор не наполняется", async () => {
    mockedFetchIds.mockResolvedValue({ items: [], total: 9000, capped: true });

    renderPage();
    selectVisibleRow();
    expect(screen.getByText(/Выбрано публикаций: 1/)).toBeInTheDocument();

    const selectAll = screen.getByRole("button", { name: "Выбрать все по фильтру" });
    await act(async () => {
      fireEvent.click(selectAll);
    });

    await waitFor(() => expect(toastError).toHaveBeenCalled());
    // Выбор не изменился (осталась 1 видимая строка), capped не наполняет.
    expect(screen.getByText(/Выбрано публикаций: 1/)).toBeInTheDocument();
  });

  it("bulk-диалог показывает N и сводку активного фильтра при выборе по фильтру", async () => {
    mockedFetchIds.mockResolvedValue({
      items: [
        {
          infobaseId: "ib-1",
          publicationId: "pub-1",
          infobaseName: "База 1",
          siteName: "S",
          virtualPath: "/1",
        },
        {
          infobaseId: "ib-2",
          publicationId: "pub-2",
          infobaseName: "База 2",
          siteName: "S",
          virtualPath: "/2",
        },
      ],
      total: 2,
      capped: false,
    });

    renderPage();
    selectVisibleRow();

    await act(async () => {
      fireEvent.click(screen.getByRole("button", { name: "Выбрать все по фильтру" }));
    });
    await waitFor(() => expect(screen.getByText(/Выбрано публикаций: 2/)).toBeInTheDocument());

    // Открываем диалог публикации.
    act(() => {
      fireEvent.click(screen.getByRole("button", { name: "Опубликовать выбранные" }));
    });

    const dialog = await screen.findByRole("dialog");
    // N виден в кнопке подтверждения и теле.
    expect(
      within(dialog).getByText(/Действие применится ко всем базам по фильтру \(2\)/)
    ).toBeInTheDocument();
    // Сводка фильтра: клиент A (фильтр по клиенту активен из URL не задан → «все базы»).
    expect(within(dialog).getByText(/все базы/)).toBeInTheDocument();
  });
});
