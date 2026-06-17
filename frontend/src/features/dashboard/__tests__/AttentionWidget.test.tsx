import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { render, screen } from "@testing-library/react";
import { MemoryRouter } from "react-router";
import { beforeEach, describe, expect, it, vi } from "vitest";
import "@/i18n";
import { AttentionWidget } from "../AttentionWidget";
import type { DashboardAlertsResponse, DashboardSummaryResponse } from "../types";
import type { CurrentUser } from "@/features/auth/useAuth";

// Виджет тянет два хука: useDashboardAlerts (агрегат сигналов) и useMe (роль для
// гейтинга Admin-only ссылок). Мокаем оба напрямую — тест сфокусирован на логике
// рендера строк/вариантов/ссылок, а не на сетевой границе.
const alertsState: {
  data: DashboardAlertsResponse | undefined;
  isLoading: boolean;
} = { data: undefined, isLoading: false };

let meData: CurrentUser | undefined;

vi.mock("../useDashboardAlerts", () => ({
  useDashboardAlerts: () => alertsState,
}));

vi.mock("@/features/auth/useAuth", () => ({
  useMe: () => ({ data: meData }),
}));

const baseAlerts: DashboardAlertsResponse = {
  quotaExceeded: 0,
  quotaAtLimit: 0,
  quotaNearLimit: 0,
  clusterDrift: { available: true, unassignedBases: 0, basesNotInCluster: 0 },
  backupDisk: { configured: true, freeBytes: 1_000_000, safetyMarginBytes: 100_000, low: false },
};

const healthySummary: DashboardSummaryResponse = {
  tenantsTotal: 2,
  tenantsActive: 2,
  infobasesTotal: 5,
  sessionsActiveTotal: 3,
  licensesConsumedTotal: 3,
  licensesAvailableTotal: 8,
  licenseFactAvailable: true,
  topTenantsByConsumption: [],
  ras: {
    healthy: true,
    lastCheckedAtUtc: "2026-06-10T12:00:00Z",
    lastErrorMessage: null,
    consecutiveFailures: 0,
  },
};

function renderWidget(summary: DashboardSummaryResponse | undefined = healthySummary) {
  const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return render(
    <QueryClientProvider client={client}>
      <MemoryRouter>
        <AttentionWidget summary={summary} />
      </MemoryRouter>
    </QueryClientProvider>
  );
}

describe("AttentionWidget (MLC-186b)", () => {
  beforeEach(() => {
    alertsState.data = { ...baseAlerts };
    alertsState.isLoading = false;
    meData = { userName: "admin", roles: ["Admin"], mustChangePassword: false };
  });

  it("пусто + данные загружены → success-строка «Всё в порядке»", () => {
    renderWidget();
    expect(screen.getByText("Всё в порядке")).toBeInTheDocument();
  });

  it("quotaExceeded → danger-строка со ссылкой на /tenants", () => {
    alertsState.data = { ...baseAlerts, quotaExceeded: 2 };
    renderWidget();
    const row = screen.getByText("2 клиента превысили квоту лицензий");
    expect(row.closest("a")).toHaveAttribute("href", "/tenants");
  });

  it("quotaAtLimit → danger-строка со ссылкой на /tenants", () => {
    alertsState.data = { ...baseAlerts, quotaAtLimit: 1 };
    renderWidget();
    const row = screen.getByText("1 клиент достиг лимита лицензий");
    expect(row.closest("a")).toHaveAttribute("href", "/tenants");
  });

  it("quotaNearLimit → warning-строка со ссылкой на /tenants", () => {
    alertsState.data = { ...baseAlerts, quotaNearLimit: 1 };
    renderWidget();
    const row = screen.getByText("1 клиент близок к лимиту лицензий");
    expect(row.closest("a")).toHaveAttribute("href", "/tenants");
  });

  it("все три бакета квоты → 3 строки в порядке превышение → достигнут → близко", () => {
    alertsState.data = {
      ...baseAlerts,
      quotaExceeded: 1,
      quotaAtLimit: 2,
      quotaNearLimit: 3,
    };
    renderWidget();
    const texts = screen.getAllByText(/лицензий$/).map((el) => el.textContent);
    expect(texts).toEqual([
      "1 клиент превысил квоту лицензий",
      "2 клиента достигли лимита лицензий",
      "3 клиента близки к лимиту лицензий",
    ]);
  });

  it("дрейф кластера → строки со ссылкой на /infobases", () => {
    alertsState.data = {
      ...baseAlerts,
      clusterDrift: { available: true, unassignedBases: 3, basesNotInCluster: 1 },
    };
    renderWidget();
    expect(screen.getByText("1 база не найдена в кластере").closest("a")).toHaveAttribute(
      "href",
      "/infobases"
    );
    expect(screen.getByText("3 нераспределённые базы в кластере").closest("a")).toHaveAttribute(
      "href",
      "/infobases"
    );
  });

  it("не-Admin: clusterDrift === null → строк дрейфа нет", () => {
    meData = { userName: "viewer", roles: ["Viewer"], mustChangePassword: false };
    alertsState.data = { ...baseAlerts, clusterDrift: null };
    renderWidget();
    expect(screen.queryByText(/в кластере/)).not.toBeInTheDocument();
  });

  it("мало места на диске бэкапов: Admin видит ссылку на /settings", () => {
    alertsState.data = {
      ...baseAlerts,
      backupDisk: { configured: true, freeBytes: 10, safetyMarginBytes: 100, low: true },
    };
    renderWidget();
    expect(screen.getByText("Мало места на диске бэкапов").closest("a")).toHaveAttribute(
      "href",
      "/settings"
    );
  });

  it("мало места: не-Admin видит строку без ссылки (Admin-only переход)", () => {
    meData = { userName: "viewer", roles: ["Viewer"], mustChangePassword: false };
    alertsState.data = {
      ...baseAlerts,
      backupDisk: { configured: true, freeBytes: 10, safetyMarginBytes: 100, low: true },
    };
    renderWidget();
    expect(screen.getByText("Мало места на диске бэкапов").closest("a")).toBeNull();
  });

  it("RAS недоступен (healthy=false, проверка была) → danger-строка, Admin → /settings", () => {
    renderWidget({
      ...healthySummary,
      ras: {
        healthy: false,
        lastCheckedAtUtc: "2026-06-10T12:00:00Z",
        lastErrorMessage: "rac.exe не найден",
        consecutiveFailures: 2,
      },
    });
    expect(screen.getByText("Нет связи с кластером 1С (RAS)").closest("a")).toHaveAttribute(
      "href",
      "/settings"
    );
  });

  it("RAS ещё не проверялся (lastCheckedAtUtc отсутствует) → строки нет", () => {
    renderWidget({
      ...healthySummary,
      ras: {
        healthy: false,
        lastCheckedAtUtc: null,
        lastErrorMessage: null,
        consecutiveFailures: 0,
      },
    });
    expect(screen.queryByText("Нет связи с кластером 1С (RAS)")).not.toBeInTheDocument();
    // только этот сигнал отсутствует → виджет «Всё в порядке»
    expect(screen.getByText("Всё в порядке")).toBeInTheDocument();
  });

  it("факт лицензий недоступен → info-строка без ссылки", () => {
    renderWidget({ ...healthySummary, licenseFactAvailable: false });
    const row = screen.getByText("Данные о лицензиях недоступны");
    expect(row.closest("a")).toBeNull();
  });

  it("isLoading → скелетоны, без строк сигналов", () => {
    alertsState.data = undefined;
    alertsState.isLoading = true;
    const { container } = renderWidget(undefined);
    expect(screen.queryByText("Всё в порядке")).not.toBeInTheDocument();
    expect(container.querySelectorAll('[data-slot="skeleton"]').length).toBeGreaterThan(0);
  });
});
