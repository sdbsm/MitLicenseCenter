import { describe, it, expect, vi, beforeEach } from "vitest";
import { render, screen, fireEvent } from "@testing-library/react";
import "@/i18n";
import type * as RasServiceModule from "../useRasService";
import type { RasServiceStatus } from "../useRasService";

// useMe — Admin по умолчанию (блок Admin-only). Переопределяется в тесте Viewer.
const meMock = vi.fn(() => ({ data: { roles: ["Admin"] } }));
vi.mock("@/features/auth/useAuth", () => ({ useMe: () => meMock() }));

// useRasServiceStatus — управляемый мок: возвращает заданный статус. enabled-флаг
// (ленивость) проверяем отдельно по факту, что до «Проверить» запрос не считается
// выполненным (data=undefined → блок показывает только кнопку).
const statusMock = vi.fn();
const refetchMock = vi.fn();
vi.mock("../useRasService", async (importOriginal) => {
  const actual = await importOriginal<typeof RasServiceModule>();
  return {
    ...actual,
    useRasServiceStatus: (enabled: boolean) => statusMock(enabled),
    useRasServiceOperation: () => ({ mutateAsync: vi.fn(), isPending: false }),
  };
});

import { RasServiceCard } from "../RasServiceCard";

function withStatus(
  status: RasServiceStatus | undefined,
  opts: Partial<{ isFetching: boolean; isError: boolean }> = {}
) {
  statusMock.mockImplementation((enabled: boolean) => ({
    // До enabled=true данных нет (ленивость): запрос «не выполнялся».
    data: enabled ? status : undefined,
    isFetching: opts.isFetching ?? false,
    isError: opts.isError ?? false,
    refetch: refetchMock,
  }));
}

describe("RasServiceCard (MLC-160, ADR-47)", () => {
  beforeEach(() => {
    meMock.mockReturnValue({ data: { roles: ["Admin"] } });
    statusMock.mockReset();
    refetchMock.mockReset();
  });

  it("Viewer не видит блок (Admin-only)", () => {
    meMock.mockReturnValue({ data: { roles: ["Viewer"] } });
    withStatus(undefined);
    const { container } = render(<RasServiceCard />);
    expect(container).toBeEmptyDOMElement();
  });

  it("ленивость: до «Проверить состояние» статус не загружается (enabled=false)", () => {
    withStatus({ state: "Ok", targetReady: true } as RasServiceStatus);
    render(<RasServiceCard />);
    // Кнопка есть, но статуса/бейджа ещё нет — enabled=false, data не пришла.
    expect(screen.getByRole("button", { name: "Проверить состояние" })).toBeInTheDocument();
    expect(screen.queryByText("Работает")).not.toBeInTheDocument();
  });

  it("по клику «Проверить» включается запрос и показывается зелёный бейдж Ok без кнопок", () => {
    withStatus({
      state: "Ok",
      targetReady: true,
      service: {
        serviceName: "MitLicenseRas",
        isRunning: true,
        binPath: null,
        platformVersion: "8.3.23.1865",
        port: "1545",
      },
    } as RasServiceStatus);
    render(<RasServiceCard />);
    fireEvent.click(screen.getByRole("button", { name: "Проверить состояние" }));

    const badge = screen.getByText("Работает");
    expect(badge).toHaveAttribute("data-variant", "success");
    expect(screen.getByText("MitLicenseRas")).toBeInTheDocument();
    // Ok — лечащих кнопок нет.
    expect(
      screen.queryByRole("button", { name: /Зарегистрировать|Обновить|Запустить/ })
    ).not.toBeInTheDocument();
  });

  it("NotRegistered → кнопка «Зарегистрировать службу RAS»", () => {
    withStatus({
      state: "NotRegistered",
      targetReady: true,
      commandPreview: "sc create ...",
    } as RasServiceStatus);
    render(<RasServiceCard />);
    fireEvent.click(screen.getByRole("button", { name: "Проверить состояние" }));
    expect(screen.getByText("Не зарегистрирована")).toHaveAttribute("data-variant", "warning");
    expect(screen.getByRole("button", { name: "Зарегистрировать службу RAS" })).toBeEnabled();
  });

  it("Outdated → кнопка с целевой версией + пояснение об устаревании", () => {
    withStatus({
      state: "Outdated",
      targetReady: true,
      service: {
        serviceName: "RAS1C",
        isRunning: true,
        binPath: null,
        platformVersion: "8.3.20.1000",
        port: "1545",
      },
      target: {
        rasExePath: "x",
        platformVersion: "8.5.1.1302",
        port: "1545",
        agentAddress: "localhost:1540",
      },
      commandPreview: "sc config ...",
    } as RasServiceStatus);
    render(<RasServiceCard />);
    fireEvent.click(screen.getByRole("button", { name: "Проверить состояние" }));
    expect(
      screen.getByRole("button", { name: "Обновить службу на платформу 8.5.1.1302" })
    ).toBeInTheDocument();
    expect(screen.getByText(/устаревшие параметры/)).toBeInTheDocument();
  });

  it("Stopped → кнопка «Запустить»", () => {
    withStatus({
      state: "Stopped",
      targetReady: true,
      service: {
        serviceName: "RAS1C",
        isRunning: false,
        binPath: null,
        platformVersion: null,
        port: null,
      },
      commandPreview: "sc start RAS1C",
    } as RasServiceStatus);
    render(<RasServiceCard />);
    fireEvent.click(screen.getByRole("button", { name: "Проверить состояние" }));
    expect(screen.getByText("Остановлена")).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "Запустить" })).toBeEnabled();
  });

  it("targetReady=false → issue показан, лечащая кнопка заблокирована", () => {
    withStatus({
      state: "NotRegistered",
      targetReady: false,
      issue: "Не выбрана платформа 1С.",
    } as RasServiceStatus);
    render(<RasServiceCard />);
    fireEvent.click(screen.getByRole("button", { name: "Проверить состояние" }));
    expect(screen.getByText("Не выбрана платформа 1С.")).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "Зарегистрировать службу RAS" })).toBeDisabled();
  });

  it("повторное «Проверить состояние» форсит refetch", () => {
    withStatus({ state: "Ok", targetReady: true } as RasServiceStatus);
    render(<RasServiceCard />);
    const btn = screen.getByRole("button", { name: "Проверить состояние" });
    fireEvent.click(btn); // первый клик — включает enabled
    fireEvent.click(btn); // второй — форс-перезапрос
    expect(refetchMock).toHaveBeenCalled();
  });
});
