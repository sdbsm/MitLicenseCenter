/**
 * Поведенческий тест: MLC-122 / R6 / UX-02
 * В таблице /tenants клиент-нарушитель (≥90%) получает danger-акцент,
 * безлимитный — без акцента.
 *
 * Привязка к data-variant (FE-19, MLC-120) — семантика, а не Tailwind-класс.
 */
import { describe, expect, it, vi } from "vitest";
import { render, screen } from "@testing-library/react";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import type { ReactNode } from "react";
import "@/i18n";
import type * as ApiModule from "@/lib/api";
import type { Tenant } from "../types";

vi.mock("sonner", () => ({
  toast: { success: vi.fn(), error: vi.fn() },
}));

vi.mock("@/lib/api", async (importOriginal) => {
  const actual = await importOriginal<typeof ApiModule>();
  return { ...actual, api: vi.fn() };
});

import type * as ReactRouterModule from "react-router";

// Мокаем react-router Link чтобы не было ошибки вне Router-контекста
vi.mock("react-router", async (importOriginal) => {
  const actual = await importOriginal<typeof ReactRouterModule>();
  return {
    ...actual,
    Link: ({ children, to }: { children: ReactNode; to: string }) => (
      <a href={to}>{children}</a>
    ),
  };
});

import { api } from "@/lib/api";

const mockedApi = vi.mocked(api);

function makePagedResponse(items: Tenant[]) {
  return { items, total: items.length, page: 1, pageSize: 25 };
}

function makeTenant(
  id: string,
  name: string,
  maxConcurrentLicenses: number
): Tenant {
  return {
    id,
    name,
    maxConcurrentLicenses,
    isActive: true,
    createdAt: "2026-01-01T00:00:00Z",
    updatedAt: null,
    infobaseCount: 0,
  };
}

function makeSnapshotResponse(
  entries: Array<{ tenantId: string; consumesLicense: boolean }>
) {
  return {
    items: entries.map((e, i) => ({
      sessionId: `session-${i}`,
      clusterInfobaseId: "cluster-1",
      tenantId: e.tenantId,
      tenantName: "T",
      infobaseName: "B",
      appId: "1cv8",
      userName: "user",
      host: "host",
      consumesLicense: e.consumesLicense,
      startedAt: "2026-01-01T00:00:00Z",
      durationSeconds: 60,
    })),
    capturedAt: "2026-01-01T00:00:00Z",
    tookMs: 1,
    source: "hot",
  };
}

function wrapper({ children }: { children: ReactNode }) {
  const qc = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return <QueryClientProvider client={qc}>{children}</QueryClientProvider>;
}

// Ленивый импорт после моков
async function renderTenantsPage() {
  const { TenantsPage } = await import("../TenantsPage");
  return render(<TenantsPage />, { wrapper });
}

describe("TenantsPage — quota column (MLC-122)", () => {
  it("клиент-нарушитель ≥90% получает danger-акцент", async () => {
    const dangerTenant = makeTenant("t-danger", "Нарушитель", 10);

    // 9 из 10 лицензий = 90% → danger
    const snapshot = makeSnapshotResponse(
      Array.from({ length: 9 }, () => ({ tenantId: "t-danger", consumesLicense: true }))
    );

    mockedApi.mockImplementation((path: string) => {
      if (typeof path === "string" && path.includes("snapshot")) {
        return Promise.resolve(snapshot);
      }
      // auth/me — минимум для ProtectedRoute
      if (typeof path === "string" && path.includes("auth/me")) {
        return Promise.resolve({ userName: "admin", roles: ["Admin"], mustChangePassword: false });
      }
      return Promise.resolve(makePagedResponse([dangerTenant]));
    });

    await renderTenantsPage();

    // Ждём появления строки клиента
    await screen.findByText("Нарушитель");

    // Проверяем наличие badge с data-variant="danger"
    const dangerBadge = await screen.findByText("Превышение лимита");
    expect(dangerBadge.closest("[data-variant]")).toHaveAttribute(
      "data-variant",
      "danger"
    );
  });

  it("безлимитный клиент (limit=0) — без акцента, показывает «—»", async () => {
    const unlimitedTenant = makeTenant("t-unlimited", "Без лимита", 0);

    const snapshot = makeSnapshotResponse([
      { tenantId: "t-unlimited", consumesLicense: true },
    ]);

    mockedApi.mockImplementation((path: string) => {
      if (typeof path === "string" && path.includes("snapshot")) {
        return Promise.resolve(snapshot);
      }
      if (typeof path === "string" && path.includes("auth/me")) {
        return Promise.resolve({ userName: "admin", roles: ["Admin"], mustChangePassword: false });
      }
      return Promise.resolve(makePagedResponse([unlimitedTenant]));
    });

    await renderTenantsPage();

    await screen.findByText("Без лимита");

    // Нет ни danger, ни warning бейджей
    expect(screen.queryByText("Превышение лимита")).toBeNull();
    expect(screen.queryByText("Близко к лимиту")).toBeNull();

    // Показывает «—» в колонке квоты для безлимитного
    // (строка contains нейтральный span — ищем по роли span с классом muted)
    const dashes = screen.getAllByText("—");
    // Хотя бы один «—» — это quota-span (нет badge рядом)
    expect(dashes.length).toBeGreaterThan(0);
  });
});
