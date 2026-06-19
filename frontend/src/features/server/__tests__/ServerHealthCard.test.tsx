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

// Нейтральные (всё в норме) ответы обслуживания — сигнал «обслуживание» не поднимается.
const okBackups = { status: "Ok", databases: [] };
const okPlans = { status: "Ok", plans: [] };

// Маршрутизация мока по URL: статус узла + проба бэкапов + проба планов (MLC-217). По
// умолчанию — нейтрально (без алерта обслуживания); тесты переопределяют конкретный URL.
function routeApi(url: string, overrides: { backups?: unknown; plans?: unknown } = {}): unknown {
  if (url.includes("/server/maintenance/backups")) return overrides.backups ?? okBackups;
  if (url.includes("/server/maintenance/plans")) return overrides.plans ?? okPlans;
  return status;
}

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

describe("ServerHealthCard (MLC-214/217)", () => {
  beforeEach(() => {
    mockedApi.mockReset();
    mockedApi.mockImplementation((url: string) => Promise.resolve(routeApi(url)));
  });

  it("светофор по overall + ссылка на /server + счётчик запущенных", async () => {
    renderCard();
    const badge = await screen.findByText("Есть замечания");
    expect(badge).toHaveAttribute("data-variant", "warning");
    expect(screen.getByText("Состояние сервера").closest("a")).toHaveAttribute("href", "/server");
    expect(screen.getByText("Серверов 1С запущено: 1 из 2")).toBeInTheDocument();
  });

  it("без проблем обслуживания строки-предупреждения нет", async () => {
    renderCard();
    await screen.findByText("Есть замечания");
    expect(screen.queryByText(/обслуживания|устарела|устарел/i)).not.toBeInTheDocument();
  });

  it("MLC-217: устаревший бэкап поднимает алерт обслуживания", async () => {
    mockedApi.mockImplementation((url: string) =>
      Promise.resolve(
        routeApi(url, {
          backups: {
            status: "Ok",
            databases: [
              {
                databaseName: "acme_bp",
                lastFullUtc: null,
                lastDiffUtc: null,
                lastLogUtc: null,
                isStale: true,
              },
            ],
          },
        })
      )
    );
    renderCard();
    expect(await screen.findByText("Резервная копия устарела")).toBeInTheDocument();
  });

  it("MLC-217: упавший под-план поднимает алерт обслуживания", async () => {
    mockedApi.mockImplementation((url: string) =>
      Promise.resolve(
        routeApi(url, {
          plans: {
            status: "Ok",
            plans: [
              {
                name: "Ночное обслуживание",
                subplans: [{ name: "Бэкап", hasSchedule: true, outcome: "Failed", tasks: [] }],
              },
            ],
          },
        })
      )
    );
    renderCard();
    expect(await screen.findByText("План обслуживания упал")).toBeInTheDocument();
  });

  it("MLC-217: degraded-проба (AgentUnavailable) НЕ поднимает алерт", async () => {
    mockedApi.mockImplementation((url: string) =>
      Promise.resolve(routeApi(url, { plans: { status: "AgentUnavailable", plans: [] } }))
    );
    renderCard();
    await screen.findByText("Есть замечания");
    expect(screen.queryByText("План обслуживания упал")).not.toBeInTheDocument();
  });
});
