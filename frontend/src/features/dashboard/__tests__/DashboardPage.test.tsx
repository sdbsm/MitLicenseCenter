import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { render, screen, waitFor } from "@testing-library/react";
import { MemoryRouter } from "react-router";
import { beforeEach, describe, expect, it, vi } from "vitest";
import "@/i18n";
import { DashboardPage } from "../DashboardPage";
import type { DashboardAlertsResponse, DashboardSummaryResponse } from "../types";
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
  licenseFactAvailable: true,
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

// MLC-186b — DashboardPage теперь рендерит AttentionWidget, который дёргает
// /dashboard/alerts. Нейтральная заглушка (всё в норме): сигналов не добавляет,
// существующие проверки страницы не затрагивает.
const alerts: DashboardAlertsResponse = {
  quotaExceeded: 0,
  quotaAtLimit: 0,
  quotaNearLimit: 0,
  clusterDrift: { available: true, unassignedBases: 0, basesNotInCluster: 0 },
  backupDisk: { configured: true, freeBytes: 1_000_000, safetyMarginBytes: 100_000, low: false },
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

// MLC-186c — «Обзор» теперь грузит трендовые отчёты лицензий/размера баз. Пустые
// 200-ответы (ряд накапливается) держат тренд-карточки в empty-state и не трогают
// существующие проверки страницы.
const emptyLicenseUsage = {
  buckets: [],
  fromUtc: "2026-06-10T00:00:00Z",
  toUtc: "2026-06-17T00:00:00Z",
  peakConsumed: 0,
  peakLimit: 0,
  averageConsumed: 0,
  clamped: false,
  maxSpanDays: 31,
};

const emptyDatabaseSize = {
  points: [],
  tenants: [],
  fromUtc: "2026-06-10T00:00:00Z",
  toUtc: "2026-06-17T00:00:00Z",
  clamped: false,
  maxSpanDays: 31,
};

// MLC-186d — «Обзор» теперь грузит последние записи аудита (RecentActivityCard).
// Пустая страница держит ленту в empty-state и не трогает существующие проверки.
const emptyAudit = { items: [], total: 0, page: 1, pageSize: 25 };

// Маршрутизация мок-ответов по URL: host/alerts/reports/audit → свои заглушки,
// остальное → summary. Переиспользуется во всех тестах (включая RAS-сбойные сценарии).
function resolveUrl(url: string, summaryResponse: unknown): unknown {
  if (url.includes("/performance/host")) return host;
  if (url.includes("/dashboard/alerts")) return alerts;
  if (url.includes("/reports/license-usage")) return emptyLicenseUsage;
  if (url.includes("/reports/database-size")) return emptyDatabaseSize;
  if (url.includes("/audit")) return emptyAudit;
  return summaryResponse;
}

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
    mockedApi.mockImplementation((url: string) => Promise.resolve(resolveUrl(url, summary)));
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
    expectHref("Использовано лицензий", "/sessions?view=usage");
    expectHref("Свободно лицензий", "/sessions?view=usage");
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
          : resolveUrl(url, summary)
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
        resolveUrl(url, {
          ...summary,
          ras: {
            healthy: false,
            lastCheckedAtUtc: "2026-06-10T12:00:00Z",
            lastErrorMessage: "rac.exe не найден по указанному пути.",
            consecutiveFailures: 3,
          },
        })
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

  // MLC-161 (инвариант ADR-47): сигнал недоступности RAS на дашборде питается
  // ТОЛЬКО дешёвым health-снимком `summary.ras`. Дорогой `/ras-service/status`
  // (перебор всех служб Windows) с дашборда дёргать нельзя — это дом Настроек.
  it("сигнал недоступности RAS НЕ вызывает дорогой /ras-service/status", async () => {
    mockedApi.mockImplementation((url: string) =>
      Promise.resolve(
        resolveUrl(url, {
          ...summary,
          ras: {
            healthy: false,
            lastCheckedAtUtc: "2026-06-10T12:00:00Z",
            lastErrorMessage: "rac.exe не найден по указанному пути.",
            consecutiveFailures: 2,
          },
        })
      )
    );
    renderPage();

    // Дожидаемся, пока сигнал отрисован (значит, дашборд отработал свои запросы).
    await waitFor(() => expect(screen.getByText("Открыть «Параметры»")).toBeInTheDocument());

    const calledUrls = mockedApi.mock.calls.map((call) => String(call[0]));
    expect(calledUrls.some((url) => url.includes("/api/v1/dashboard/summary"))).toBe(true);
    expect(calledUrls.some((url) => url.includes("/ras-service/status"))).toBe(false);
  });

  // MLC-186c — «живой» индикатор на KPI «Активные сеансы» (число опрашивается онлайн).
  it("KPI «Активные сеансы» показывает live-индикатор", async () => {
    renderPage();
    await waitFor(() => expect(screen.getByText("Топ клиентов по нагрузке")).toBeInTheDocument());

    const sessionsCard = screen.getByText("Активные сеансы").closest("a");
    expect(sessionsCard?.querySelector('[data-testid="kpi-live-dot"]')).toBeInTheDocument();
    // На прочих KPI индикатора нет.
    expect(
      screen
        .getByText("Использовано лицензий")
        .closest("a")
        ?.querySelector('[data-testid="kpi-live-dot"]')
    ).toBeNull();
  });

  // MLC-186c — спарклайн под KPI «Использовано лицензий» при накопленном ряде.
  it("KPI «Использовано лицензий» рендерит спарклайн при наличии buckets", async () => {
    mockedApi.mockImplementation((url: string) =>
      Promise.resolve(
        url.includes("/reports/license-usage")
          ? {
              ...emptyLicenseUsage,
              buckets: [
                {
                  bucketStartUtc: "2026-06-10T00:00:00Z",
                  consumedAvg: 2,
                  consumedMax: 4,
                  limit: 10,
                },
                {
                  bucketStartUtc: "2026-06-11T00:00:00Z",
                  consumedAvg: 3,
                  consumedMax: 6,
                  limit: 10,
                },
              ],
              peakConsumed: 6,
              peakLimit: 10,
            }
          : resolveUrl(url, summary)
      )
    );
    renderPage();

    await waitFor(() =>
      expect(
        screen
          .getByText("Использовано лицензий")
          .closest("a")
          ?.querySelector('[data-testid="kpi-sparkline"]')
      ).toBeInTheDocument()
    );
  });

  it("KPI «Использовано лицензий» без buckets — спарклайна нет", async () => {
    renderPage();
    await waitFor(() => expect(screen.getByText("Топ клиентов по нагрузке")).toBeInTheDocument());
    expect(
      screen
        .getByText("Использовано лицензий")
        .closest("a")
        ?.querySelector('[data-testid="kpi-sparkline"]')
    ).toBeNull();
  });
});
