import { describe, it, expect, vi, beforeEach } from "vitest";
import { render, screen, waitFor } from "@testing-library/react";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import type { ReactNode } from "react";
import "@/i18n";
import type * as ApiModule from "@/lib/api";
import { SettingsPage } from "../SettingsPage";
import type { SettingDescriptor } from "../types";

// Частичный мок: подменяем только сетевой `api`, остальное (ApiError) — настоящее.
vi.mock("@/lib/api", async (importOriginal) => {
  const actual = await importOriginal<typeof ApiModule>();
  return { ...actual, api: vi.fn() };
});

import { api } from "@/lib/api";

const mockedApi = vi.mocked(api);

function descriptor(key: string, value: string | null): SettingDescriptor {
  return {
    key,
    isSecret: false,
    isSet: value !== null,
    value,
    description: null,
    updatedAt: "2026-01-01T00:00:00Z",
    updatedBy: "System",
  };
}

function setup() {
  const client = new QueryClient({
    defaultOptions: { queries: { retry: false }, mutations: { retry: false } },
  });
  const wrapper = ({ children }: { children: ReactNode }) => (
    <QueryClientProvider client={client}>{children}</QueryClientProvider>
  );
  render(<SettingsPage />, { wrapper });
}

describe("SettingsPage — RAS-порт и пикер платформы (MLC-055)", () => {
  beforeEach(() => {
    mockedApi.mockReset();
  });

  // Мок ветвится по URL: список настроек отдаёт дескрипторы, discovery-эндпоинты —
  // DiscoveryResponse (страница дёргает useRacPaths на маунте).
  function mockApiByUrl() {
    mockedApi.mockImplementation((async (url: string) => {
      if (url.startsWith("/api/v1/discovery/")) {
        return { items: [], available: false, error: null };
      }
      return [
        descriptor("OneC.RAS.Endpoint", "localhost:1600"),
        descriptor("OneC.RAS.ExePath", "C:\\Program Files\\1cv8\\8.3.23.1865\\bin\\rac.exe"),
        descriptor("OneC.DefaultPlatformVersion", "8.3.23.1865"),
      ];
    }) as unknown as typeof api);
  }

  it("RAS endpoint показывается как поле «Порт» с разобранным значением", async () => {
    mockApiByUrl();
    setup();

    expect(await screen.findByText("Порт RAS")).toBeInTheDocument();
    const portInput = await waitFor(() => screen.getByLabelText("Порт RAS"));
    expect(portInput).toHaveValue(1600);
  });

  it("единый пикер платформы показывает текущий путь rac.exe и версию по умолчанию", async () => {
    mockApiByUrl();
    setup();

    expect(await screen.findByText("Платформа 1С")).toBeInTheDocument();
    // Строка текущего состояния отражает оба раздельных ключа.
    expect(
      await screen.findByText(/rac\.exe.*8\.3\.23\.1865\\bin\\rac\.exe.*версия.*8\.3\.23\.1865/)
    ).toBeInTheDocument();
  });
});
