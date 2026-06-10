import { describe, it, expect, vi, beforeEach } from "vitest";
import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import type { ReactNode } from "react";
import "@/i18n";
import type * as ApiModule from "@/lib/api";
import { UnassignedInfobasesDialog } from "../UnassignedInfobasesDialog";
import type { HiddenUnassignedInfobase, UnassignedInfobaseItem } from "../types";

vi.mock("sonner", () => ({ toast: { success: vi.fn(), error: vi.fn() } }));

vi.mock("@/lib/api", async (importOriginal) => {
  const actual = await importOriginal<typeof ApiModule>();
  return { ...actual, api: vi.fn() };
});

import { api } from "@/lib/api";

const mockedApi = vi.mocked(api);

const item: UnassignedInfobaseItem = {
  clusterInfobaseId: "11111111-1111-1111-1111-111111111111",
  name: "bd1",
  description: "Бухгалтерия",
};

const hidden: HiddenUnassignedInfobase = {
  clusterInfobaseId: "22222222-2222-2222-2222-222222222222",
  name: "service",
  hiddenAtUtc: "2026-06-11T10:00:00Z",
  hiddenBy: "admin",
};

function setup(props?: Partial<Parameters<typeof UnassignedInfobasesDialog>[0]>) {
  const client = new QueryClient({
    defaultOptions: { queries: { retry: false }, mutations: { retry: false } },
  });
  const onAssign = vi.fn();
  const onManualEntry = vi.fn();
  const onRefresh = vi.fn();
  const wrapper = ({ children }: { children: ReactNode }) => (
    <QueryClientProvider client={client}>{children}</QueryClientProvider>
  );
  render(
    <UnassignedInfobasesDialog
      open
      onOpenChange={vi.fn()}
      items={[item]}
      hiddenItems={[hidden]}
      available
      checkedAtUtc="2026-06-11T10:00:00Z"
      isLoading={false}
      isRefreshing={false}
      onRefresh={onRefresh}
      onAssign={onAssign}
      onManualEntry={onManualEntry}
      {...props}
    />,
    { wrapper }
  );
  return { onAssign, onManualEntry, onRefresh, user: userEvent.setup() };
}

describe("UnassignedInfobasesDialog", () => {
  beforeEach(() => {
    mockedApi.mockReset();
    mockedApi.mockResolvedValue(null);
  });

  it("показывает имя и UUID базы; «Назначить» вызывает onAssign с этой базой", async () => {
    const { onAssign, user } = setup();

    expect(screen.getByText("bd1")).toBeInTheDocument();
    expect(screen.getByText(item.clusterInfobaseId)).toBeInTheDocument();

    await user.click(screen.getByRole("button", { name: "Назначить" }));
    expect(onAssign).toHaveBeenCalledWith(item);
  });

  it("«Скрыть» шлёт POST .../hide со снапшотом имени", async () => {
    const { user } = setup();

    await user.click(screen.getByRole("button", { name: "Скрыть" }));

    expect(mockedApi).toHaveBeenCalledWith(
      `/api/v1/infobases/unassigned/${item.clusterInfobaseId}/hide`,
      expect.objectContaining({ method: "POST", body: { name: "bd1" } })
    );
  });

  it("блок «Скрытые» раскрывается; «Вернуть» шлёт DELETE .../hide", async () => {
    const { user } = setup();

    // До раскрытия скрытая база не видна.
    expect(screen.queryByText("service")).not.toBeInTheDocument();
    await user.click(screen.getByRole("button", { name: /Скрытые: 1/ }));
    expect(screen.getByText("service")).toBeInTheDocument();

    await user.click(screen.getByRole("button", { name: "Вернуть" }));
    expect(mockedApi).toHaveBeenCalledWith(
      `/api/v1/infobases/unassigned/${hidden.clusterInfobaseId}/hide`,
      expect.objectContaining({ method: "DELETE" })
    );
  });

  it("«Ввести вручную» вызывает onManualEntry (fallback на форму)", async () => {
    const { onManualEntry, user } = setup();
    await user.click(screen.getByRole("button", { name: "Ввести вручную" }));
    expect(onManualEntry).toHaveBeenCalled();
  });

  it("пустое состояние, когда RAS доступен, но баз нет", () => {
    setup({ items: [], hiddenItems: [] });
    expect(screen.getByText("Все базы разобраны")).toBeInTheDocument();
  });

  it("при available:false показывает заметку о недоступном RAS, но блок скрытых остаётся", () => {
    setup({ available: false, items: [] });
    expect(screen.getByText(/Кластер 1С недоступен/)).toBeInTheDocument();
    expect(screen.getByRole("button", { name: /Скрытые: 1/ })).toBeInTheDocument();
  });
});
