import { describe, it, expect, vi, beforeEach, beforeAll } from "vitest";
import { render, screen, waitFor, within } from "@testing-library/react";
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

// jsdom не реализует IntersectionObserver — scroll-spy навигации (MLC-202) его требует.
// Достаточно безоперационной заглушки: тест проверяет разметку якорей, не подсветку.
beforeAll(() => {
  class IOStub {
    observe() {}
    unobserve() {}
    disconnect() {}
    takeRecords() {
      return [];
    }
  }
  vi.stubGlobal("IntersectionObserver", IOStub);
});

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

describe("SettingsPage — левая навигация по секциям-якорям (MLC-202)", () => {
  beforeEach(() => {
    mockedApi.mockReset();
  });

  // Минимально достаточный мок: список настроек + пустой discovery. Навигация
  // рендерится из статического массива якорей, не зависит от состава дескрипторов.
  function mockApi() {
    mockedApi.mockImplementation((async (url: string) => {
      if (url.startsWith("/api/v1/discovery/")) {
        return { items: [], available: false, error: null };
      }
      return [
        descriptor("OneC.RAS.Endpoint", "localhost:1545"),
        descriptor("OneC.RAS.ExePath", "C:\\1cv8\\bin\\rac.exe"),
        descriptor("OneC.DefaultPlatformVersion", "8.3.23.1865"),
      ];
    }) as unknown as typeof api);
  }

  // Восемь якорей в целевом порядке: подпись (settings.nav.*) → целевой #id.
  const EXPECTED_ANCHORS: { label: string; href: string }[] = [
    { label: "Подключение", href: "#settings-cluster" },
    { label: "SQL", href: "#settings-sql" },
    { label: "IIS", href: "#settings-iis" },
    { label: "Опрос", href: "#settings-polling" },
    { label: "Хранение", href: "#settings-retention" },
    { label: "Бэкапы", href: "#settings-backup" },
    { label: "Служба RAS", href: "#settings-ras" },
    { label: "Обновления", href: "#settings-updates" },
  ];

  it("рендерит 8 пунктов навигации-якорей с правильными подписями и ссылками", async () => {
    mockApi();
    setup();

    // Навигация параметров — отдельный landmark <nav aria-label="Параметры">.
    const nav = await screen.findByRole("navigation", { name: "Параметры" });
    const links = within(nav).getAllByRole("link");
    expect(links).toHaveLength(EXPECTED_ANCHORS.length);

    EXPECTED_ANCHORS.forEach((expected, i) => {
      expect(links[i]).toHaveTextContent(expected.label);
      expect(links[i]).toHaveAttribute("href", expected.href);
    });
  });
});
