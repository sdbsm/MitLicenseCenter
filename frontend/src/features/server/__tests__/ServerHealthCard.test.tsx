import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { render, screen } from "@testing-library/react";
import { MemoryRouter } from "react-router";
import { beforeEach, describe, expect, it, vi } from "vitest";
import "@/i18n";
import type * as ApiModule from "@/lib/api";
import { ServerHealthCard } from "../ServerHealthCard";
import type { ServerStatus } from "../useServerStatus";

vi.mock("@/lib/api", async (importOriginal) => {
  const actual = await importOriginal<typeof ApiModule>();
  return { ...actual, api: vi.fn() };
});

import { api } from "@/lib/api";

const mockedApi = vi.mocked(api);

const status: ServerStatus = {
  oneCServers: [
    { serviceName: "a", running: true, platformVersion: null },
    { serviceName: "b", running: false, platformVersion: null },
  ],
  ras: { state: "Ok", running: true, serviceName: null, available: true, error: null },
  sql: { instance: null, serviceName: null, running: true, available: true, error: null },
  iis: { state: "Started", available: true, error: null },
  overall: "Degraded",
};

function renderCard() {
  const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return render(
    <QueryClientProvider client={client}>
      <MemoryRouter>
        <ServerHealthCard />
      </MemoryRouter>
    </QueryClientProvider>
  );
}

describe("ServerHealthCard (MLC-214)", () => {
  beforeEach(() => {
    mockedApi.mockReset();
    mockedApi.mockResolvedValue(status);
  });

  it("светофор по overall + ссылка на /server + счётчик запущенных", async () => {
    renderCard();
    const badge = await screen.findByText("Есть замечания");
    expect(badge).toHaveAttribute("data-variant", "warning");
    expect(screen.getByText("Состояние сервера").closest("a")).toHaveAttribute("href", "/server");
    expect(screen.getByText("Серверов 1С запущено: 1 из 2")).toBeInTheDocument();
  });
});
