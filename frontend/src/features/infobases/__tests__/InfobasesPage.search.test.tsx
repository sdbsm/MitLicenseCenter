import { describe, it, expect, vi, beforeEach, afterEach } from "vitest";
import { render, screen, act, fireEvent } from "@testing-library/react";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { MemoryRouter } from "react-router";
import type { ReactNode } from "react";
import "@/i18n";
import type * as UseInfobasesModule from "../useInfobases";

// Тяжёлые дочерние диалоги/карточки заглушаем — тест про поиск, не про них.
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

// search — последний (6-й) аргумент useInfobases; page — 4-й.
const SEARCH_ARG = 5;
const PAGE_ARG = 3;
const lastCall = () => mockedInfobases.mock.calls.at(-1)!;

describe("InfobasesPage — серверный поиск (MLC-181a)", () => {
  beforeEach(() => {
    vi.useFakeTimers();
    mockedMe.mockReturnValue({ data: { id: "u1", userName: "admin", roles: ["Admin"] } } as never);
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
    mockedUnassigned.mockReturnValue({
      data: { items: [], hiddenItems: [], available: false, error: null, checkedAtUtc: null },
      isLoading: false,
      isFetching: false,
      refresh: vi.fn(),
    } as never);
  });

  afterEach(() => {
    vi.runOnlyPendingTimers();
    vi.useRealTimers();
  });

  it("ввод в инпут → после debounce search-терм уходит в useInfobases (querystring ?search=)", () => {
    renderPage();
    expect(lastCall()[SEARCH_ARG]).toBe("");

    const input = screen.getByRole("searchbox", { name: /Поиск по названию или имени БД/ });
    act(() => {
      fireEvent.change(input, { target: { value: "Бухгал" } });
    });

    // Дебаунс ещё не сработал — терм не ушёл в запрос.
    expect(lastCall()[SEARCH_ARG]).toBe("");

    act(() => {
      vi.advanceTimersByTime(300);
    });

    // После debounce терм прокинут в хук → useInfobases собирает ?search=Бухгал.
    expect(mockedInfobases.mock.calls.some((c) => c[SEARCH_ARG] === "Бухгал")).toBe(true);
  });

  it("смена терма сбрасывает на первую страницу", () => {
    renderPage();

    const input = screen.getByRole("searchbox", { name: /Поиск по названию или имени БД/ });
    act(() => {
      fireEvent.change(input, { target: { value: "Зарплата" } });
    });
    act(() => {
      vi.advanceTimersByTime(300);
    });

    const after = lastCall();
    expect(after[PAGE_ARG]).toBe(1);
    expect(after[SEARCH_ARG]).toBe("Зарплата");
  });
});
