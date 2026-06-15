import { describe, it, expect, vi, beforeEach } from "vitest";
import { render, screen, fireEvent, waitFor } from "@testing-library/react";
import "@/i18n";
import { ApiError } from "@/lib/api";
import type * as RasServiceModule from "../useRasService";
import type { RasServiceStatus } from "../useRasService";

const mutateAsync = vi.fn();
vi.mock("../useRasService", async (importOriginal) => {
  const actual = await importOriginal<typeof RasServiceModule>();
  return {
    ...actual,
    useRasServiceOperation: () => ({ mutateAsync, isPending: false }),
  };
});
const toastError = vi.fn();
const toastSuccess = vi.fn();
vi.mock("sonner", () => ({
  toast: { success: (m: string) => toastSuccess(m), error: (m: string) => toastError(m) },
}));

import { RasServiceActionDialog } from "../RasServiceActionDialog";

const status: RasServiceStatus = {
  state: "NotRegistered",
  targetReady: true,
  commandPreview: 'sc create MitLicenseRas binPath= "C:\\1cv8\\ras.exe cluster" start= auto',
  target: {
    rasExePath: "C:\\Program Files\\1cv8\\8.3.23.1865\\bin\\ras.exe",
    platformVersion: "8.3.23.1865",
    port: "1545",
    agentAddress: "localhost:1540",
  },
  service: null,
  issue: null,
};

describe("RasServiceActionDialog (MLC-160, ADR-45/47)", () => {
  beforeEach(() => {
    mutateAsync.mockReset();
    toastError.mockReset();
    toastSuccess.mockReset();
  });

  it("показывает целевые параметры И точную команду sc …", () => {
    render(
      <RasServiceActionDialog open operation="register" status={status} onOpenChange={vi.fn()} />
    );
    // Человеческое описание.
    expect(screen.getByText("8.3.23.1865")).toBeInTheDocument();
    expect(screen.getByText("1545")).toBeInTheDocument();
    // Точная команда (прозрачность ADR-47).
    expect(screen.getByText(/sc create MitLicenseRas/)).toBeInTheDocument();
  });

  it("подтверждение вызывает мутацию с операцией register и тост успеха", async () => {
    mutateAsync.mockResolvedValue({ state: "Ok", serviceName: "MitLicenseRas" });
    const onOpenChange = vi.fn();
    render(
      <RasServiceActionDialog
        open
        operation="register"
        status={status}
        onOpenChange={onOpenChange}
      />
    );

    fireEvent.click(screen.getByRole("button", { name: "Зарегистрировать" }));
    await waitFor(() => expect(mutateAsync).toHaveBeenCalledWith("register"));
    await waitFor(() => expect(onOpenChange).toHaveBeenCalledWith(false));
    expect(toastSuccess).toHaveBeenCalled();
  });

  it("отмена закрывает диалог без мутации", () => {
    render(
      <RasServiceActionDialog open operation="register" status={status} onOpenChange={vi.fn()} />
    );
    fireEvent.click(screen.getByRole("button", { name: "Отмена" }));
    expect(mutateAsync).not.toHaveBeenCalled();
  });

  it("409 показывает detail из тела ProblemDetails", async () => {
    mutateAsync.mockRejectedValue(
      new ApiError(409, "conflict", {
        code: "RAS_SERVICE_OPERATION_FAILED",
        detail: "ras.exe не найден по пути.",
      })
    );
    render(
      <RasServiceActionDialog open operation="register" status={status} onOpenChange={vi.fn()} />
    );
    fireEvent.click(screen.getByRole("button", { name: "Зарегистрировать" }));
    await waitFor(() => expect(toastError).toHaveBeenCalledWith("ras.exe не найден по пути."));
  });

  it("update: заголовок и кнопка подтверждения «Обновить»", () => {
    render(
      <RasServiceActionDialog open operation="update" status={status} onOpenChange={vi.fn()} />
    );
    expect(screen.getByRole("button", { name: "Обновить" })).toBeInTheDocument();
  });
});
