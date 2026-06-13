import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { render, screen, waitFor } from "@testing-library/react";
import { MemoryRouter } from "react-router";
import { beforeEach, describe, expect, it, vi } from "vitest";
import "@/i18n";
import { DashboardPage } from "../DashboardPage";
import type { DashboardSummaryResponse } from "../types";
import type { HostMetricsSnapshot } from "@/features/performance/types";

vi.mock("@/lib/api", () => ({
  api: vi.fn(),
}));

import { api } from "@/lib/api";

const mockedApi = vi.mocked(api);

const summary: DashboardSummaryResponse = {
  tenantsTotal: 2,
  tenantsActive: 2,
  infobasesTotal: 5,
  sessionsActiveTotal: 3,
  licensesConsumedTotal: 3,
  licensesAvailableTotal: 8,
  topTenantsByConsumption: [
    { tenantId: "t-1", tenantName: "Ромашка", consumed: 2, limit: 5, percent: 40 },
  ],
  ras: {
    healthy: true,
    lastCheckedAtUtc: "2026-06-10T12:00:00Z",
    lastErrorMessage: null,
    consecutiveFailures: 0,
  },
};

const host: HostMetricsSnapshot = {
  capturedAtUtc: "2026-06-10T12:00:00Z",
  measuring: false,
  cpu: { totalPercent: 12, queueLength: 0 },
  memory: { availableMBytes: 8192, totalMBytes: 16384, pagesPerSec: 0 },
  disk: { avgReadSecPerOp: 0.002, avgWriteSecPerOp: 0.003, queueLength: 0 },
  processGroups: [],
  processesInaccessible: 0,
  attributionIncomplete: false,
};

function renderPage() {
  const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return render(
    <QueryClientProvider client={client}>
      <MemoryRouter>
        <DashboardPage />
      </MemoryRouter>
    </QueryClientProvider>
  );
}

describe("DashboardPage (MLC-085: обзор с переходами)", () => {
  beforeEach(() => {
    mockedApi.mockReset();
    mockedApi.mockImplementation((url: string) =>
      Promise.resolve(url.includes("/performance/host") ? host : (summary as unknown))
    );
  });

  it("KPI-карточки — ссылки в свои разделы", async () => {
    renderPage();
    await waitFor(() => expect(screen.getByText("Топ клиентов по нагрузке")).toBeInTheDocument());

    const expectHref = (label: string, href: string) => {
      const link = screen.getByText(label).closest("a");
      expect(link).toHaveAttribute("href", href);
    };
    expectHref("Клиенты", "/tenants");
    expectHref("Инфобазы", "/infobases");
    expectHref("Активные сеансы", "/sessions");
    expectHref("Использовано лицензий", "/reports");
    expectHref("Свободно лицензий", "/reports");
  });

  it("здоровье хоста — ссылка на /performance с тремя гейджами", async () => {
    renderPage();
    // Заголовок карточки виден и при скелетоне — ждём содержимое гейджей.
    await waitFor(() => expect(screen.getByText("Процессор")).toBeInTheDocument());

    expect(screen.getByText("Здоровье хоста").closest("a")).toHaveAttribute("href", "/performance");
    expect(screen.getByText("Процессор")).toBeInTheDocument();
    expect(screen.getByText("Память")).toBeInTheDocument();
    expect(screen.getByText("Диск (латентность)")).toBeInTheDocument();
  });

  it("первая проба (measuring=true) показывает «измеряю…», не нули", async () => {
    mockedApi.mockImplementation((url: string) =>
      Promise.resolve(
        url.includes("/performance/host")
          ? { ...host, measuring: true, cpu: { totalPercent: 0, queueLength: 0 } }
          : (summary as unknown)
      )
    );
    renderPage();
    // CPU и диск — дельта-метрики (на первой пробе «измеряю…»); RAM мгновенна.
    await waitFor(() => expect(screen.getAllByText("измеряю…")).toHaveLength(2));
    expect(screen.queryByText("0 %")).not.toBeInTheDocument();
  });

  it("имя клиента в топе — ссылка на паспорт клиента", async () => {
    renderPage();
    await waitFor(() => expect(screen.getByText("Ромашка")).toBeInTheDocument());

    expect(screen.getByText("Ромашка").closest("a")).toHaveAttribute("href", "/tenants/t-1");
  });

  it("UX-17: при !healthy RAS-карточка показывает видимую подсказку + ссылку в «Параметры»", async () => {
    mockedApi.mockImplementation((url: string) =>
      Promise.resolve(
        url.includes("/performance/host")
          ? host
          : ({
              ...summary,
              ras: {
                healthy: false,
                lastCheckedAtUtc: "2026-06-10T12:00:00Z",
                lastErrorMessage: "rac.exe не найден по указанному пути.",
                consecutiveFailures: 3,
              },
            } as unknown)
      )
    );
    renderPage();

    await waitFor(() =>
      expect(
        screen.getByText("Нет связи с кластером 1С. Проверьте адрес RAS в разделе «Параметры».")
      ).toBeInTheDocument()
    );
    // Видимая ссылка-переход в «Параметры» (а не только тултип).
    expect(screen.getByText("Открыть «Параметры»").closest("a")).toHaveAttribute(
      "href",
      "/settings"
    );
    // Счётчик ошибок подряд.
    expect(screen.getByText("3 ошибки подряд")).toBeInTheDocument();
  });

  it("при healthy подсказки нет", async () => {
    renderPage();
    await waitFor(() => expect(screen.getByText("Топ клиентов по нагрузке")).toBeInTheDocument());
    expect(
      screen.queryByText("Нет связи с кластером 1С. Проверьте адрес RAS в разделе «Параметры».")
    ).not.toBeInTheDocument();
  });
});
