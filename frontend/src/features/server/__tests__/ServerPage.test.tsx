import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { render, screen, fireEvent, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { MemoryRouter } from "react-router";
import { beforeEach, describe, expect, it, vi } from "vitest";
import "@/i18n";
import type * as ApiModule from "@/lib/api";
import { ServerPage } from "../ServerPage";
import type { ServerStatus } from "../useServerStatus";

// useMe — Admin по умолчанию; переопределяется в Viewer-тесте.
const meMock = vi.fn(() => ({ data: { roles: ["Admin"] } }));
vi.mock("@/features/auth/useAuth", () => ({ useMe: () => meMock() }));

vi.mock("@/lib/api", async (importOriginal) => {
  const actual = await importOriginal<typeof ApiModule>();
  return { ...actual, api: vi.fn() };
});

import { api } from "@/lib/api";

const mockedApi = vi.mocked(api);

const healthyStatus: ServerStatus = {
  oneCServers: [
    { serviceName: "ragent-stopped", running: false, platformVersion: "8.3.23.1865" },
    { serviceName: "ragent-running", running: true, platformVersion: null },
  ],
  ras: { state: "Ok", running: true, serviceName: "RAS1C", available: true, error: null },
  sql: {
    instance: "MSSQLSERVER",
    serviceName: "MSSQLSERVER",
    running: true,
    available: true,
    error: null,
  },
  iis: { state: "Started", available: true, error: null },
  overall: "Healthy",
};

function renderPage() {
  const client = new QueryClient({
    defaultOptions: { queries: { retry: false }, mutations: { retry: false } },
  });
  return render(
    <QueryClientProvider client={client}>
      <MemoryRouter>
        <ServerPage />
      </MemoryRouter>
    </QueryClientProvider>
  );
}

// Маршрутизация мока по URL: /server → сводный статус (вкладка «Службы»),
// /iis/* → пустые discovery-ответы (вкладка «IIS», MLC-215).
function routeApi(url: string) {
  if (url.startsWith("/api/v1/iis/server")) {
    return Promise.resolve({ state: "Started", available: true, error: null });
  }
  if (url.startsWith("/api/v1/iis/")) {
    return Promise.resolve({ items: [], available: true, error: null });
  }
  return Promise.resolve(healthyStatus);
}

describe("ServerPage (MLC-214/215)", () => {
  beforeEach(() => {
    meMock.mockReturnValue({ data: { roles: ["Admin"] } });
    mockedApi.mockReset();
    mockedApi.mockImplementation((url: string) => routeApi(url));
  });

  it("светофор показывает общее состояние по overall", async () => {
    renderPage();
    const badge = await screen.findByText("В норме");
    expect(badge).toHaveAttribute("data-variant", "success");
  });

  it("список серверов 1С со статусами и версией", async () => {
    renderPage();
    await screen.findByText("ragent-stopped");
    expect(screen.getByText("ragent-running")).toBeInTheDocument();
    // «Запущен» встречается и у бейджа службы 1С, и у IIS-сводки — ловим оба, не падаем.
    expect(screen.getAllByText("Запущен").length).toBeGreaterThanOrEqual(1);
    expect(screen.getByText("Остановлен")).toBeInTheDocument();
    expect(screen.getByText("Платформа 8.3.23.1865")).toBeInTheDocument();
  });

  it("Admin видит кнопки действий (старт для остановленного, стоп/перезапуск для запущенного)", async () => {
    renderPage();
    await screen.findByText("ragent-stopped");
    expect(screen.getByRole("button", { name: "Запустить" })).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "Остановить" })).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "Перезапустить" })).toBeInTheDocument();
  });

  it("Viewer не видит кнопок управления", async () => {
    meMock.mockReturnValue({ data: { roles: ["Viewer"] } });
    renderPage();
    await screen.findByText("ragent-stopped");
    expect(screen.queryByRole("button", { name: "Запустить" })).not.toBeInTheDocument();
    expect(screen.queryByRole("button", { name: "Остановить" })).not.toBeInTheDocument();
    expect(screen.queryByRole("button", { name: "Перезапустить" })).not.toBeInTheDocument();
  });

  it("деградация: available:false → текст ошибки, экран не падает", async () => {
    mockedApi.mockResolvedValue({
      ...healthyStatus,
      sql: {
        instance: null,
        serviceName: null,
        running: false,
        available: false,
        error: "Служба SQL недоступна.",
      },
    } satisfies ServerStatus);
    renderPage();
    expect(await screen.findByText("Служба SQL недоступна.")).toBeInTheDocument();
  });

  it("стоп открывает диалог подтверждения с предупреждением о простое баз", async () => {
    renderPage();
    await screen.findByText("ragent-running");
    fireEvent.click(screen.getByRole("button", { name: "Остановить" }));
    await waitFor(() => expect(screen.getByText("Остановить сервер 1С?")).toBeInTheDocument());
    expect(screen.getByText("Операция прервёт работу всех баз узла.")).toBeInTheDocument();
  });

  it("пустой список серверов 1С → подсказка «не обнаружен»", async () => {
    mockedApi.mockResolvedValue({ ...healthyStatus, oneCServers: [] } satisfies ServerStatus);
    renderPage();
    expect(await screen.findByText("Сервер 1С не обнаружен.")).toBeInTheDocument();
  });

  // MLC-215: дом IIS = «Сервер». Вкладка «Службы» активна по умолчанию, «IIS» —
  // монтирует карточку управления IIS только при активации.
  it("вкладка «Службы» активна по умолчанию (рендерит светофор и список 1С)", async () => {
    renderPage();
    expect(await screen.findByText("В норме")).toBeInTheDocument();
    expect(screen.getByText("ragent-running")).toBeInTheDocument();
    // Карточка IIS не смонтирована, пока вкладка «IIS» не активирована.
    expect(screen.queryByText("Управление IIS")).not.toBeInTheDocument();
  });

  it("вкладка «IIS» при активации рендерит карточку управления IIS", async () => {
    const user = userEvent.setup();
    renderPage();
    await screen.findByText("В норме");
    await user.click(screen.getByRole("tab", { name: "IIS" }));
    expect(await screen.findByText("Управление IIS")).toBeInTheDocument();
  });
});
