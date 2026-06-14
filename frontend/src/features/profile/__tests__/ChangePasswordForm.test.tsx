import { describe, it, expect, vi, beforeEach } from "vitest";
import { render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import type { ReactNode } from "react";
import "@/i18n";
import type * as ApiModule from "@/lib/api";
import { ChangePasswordForm } from "../ChangePasswordForm";

vi.mock("sonner", () => ({
  toast: { success: vi.fn(), error: vi.fn() },
}));

vi.mock("@/lib/api", async (importOriginal) => {
  const actual = await importOriginal<typeof ApiModule>();
  return { ...actual, api: vi.fn() };
});

import { api, ApiError } from "@/lib/api";
import { toast } from "sonner";

const mockedApi = vi.mocked(api);
const mockedToastSuccess = vi.mocked(toast.success);
const mockedToastError = vi.mocked(toast.error);

// Пароль ≥12 символов, удовлетворяющий zod-схеме (newPassword.min(12)).
const NEW_PASSWORD = "NewStrongPass#1";

function setup(props: Parameters<typeof ChangePasswordForm>[0] = {}) {
  const client = new QueryClient({
    defaultOptions: { queries: { retry: false }, mutations: { retry: false } },
  });
  const wrapper = ({ children }: { children: ReactNode }) => (
    <QueryClientProvider client={client}>{children}</QueryClientProvider>
  );
  render(<ChangePasswordForm {...props} />, { wrapper });
  return { u: userEvent.setup() };
}

describe("ChangePasswordForm", () => {
  beforeEach(() => {
    mockedApi.mockReset();
    mockedToastSuccess.mockReset();
    mockedToastError.mockReset();
  });

  it("happy-path: шлёт текущий+новый пароль, тостит успех и зовёт onSuccess", async () => {
    mockedApi.mockResolvedValueOnce(null);
    const onSuccess = vi.fn();
    const { u } = setup({ onSuccess });

    await u.type(screen.getByLabelText("Текущий пароль"), "OldPass#123456");
    await u.type(screen.getByLabelText("Новый пароль"), NEW_PASSWORD);
    await u.type(screen.getByLabelText("Повторите новый пароль"), NEW_PASSWORD);
    await u.click(screen.getByRole("button", { name: "Сменить пароль" }));

    await waitFor(() =>
      expect(mockedApi).toHaveBeenCalledWith("/api/v1/auth/change-password", {
        method: "POST",
        body: { currentPassword: "OldPass#123456", newPassword: NEW_PASSWORD },
      })
    );
    expect(mockedToastSuccess).toHaveBeenCalledWith("Пароль обновлён.");
    expect(onSuccess).toHaveBeenCalledTimes(1);
  });

  it("400 с field-errors маппит ошибку backend на поле формы", async () => {
    mockedApi.mockRejectedValueOnce(
      new ApiError(400, "bad request", {
        errors: { CurrentPassword: ["Текущий пароль неверен."] },
      })
    );
    const onSuccess = vi.fn();
    const { u } = setup({ onSuccess });

    await u.type(screen.getByLabelText("Текущий пароль"), "WrongPass#123");
    await u.type(screen.getByLabelText("Новый пароль"), NEW_PASSWORD);
    await u.type(screen.getByLabelText("Повторите новый пароль"), NEW_PASSWORD);
    await u.click(screen.getByRole("button", { name: "Сменить пароль" }));

    await waitFor(() => expect(screen.getByText("Текущий пароль неверен.")).toBeInTheDocument());
    expect(onSuccess).not.toHaveBeenCalled();
    expect(mockedToastError).not.toHaveBeenCalled();
  });

  describe("кнопка Отмена (UX-39)", () => {
    it("показывается и вызывает onCancel при клике", async () => {
      const onCancel = vi.fn();
      const { u } = setup({ onCancel });

      const cancelBtn = screen.getByRole("button", { name: "Отмена" });
      expect(cancelBtn).toBeInTheDocument();

      await u.click(cancelBtn);
      expect(onCancel).toHaveBeenCalledTimes(1);
      expect(mockedApi).not.toHaveBeenCalled();
    });

    it("не показывается когда onCancel не передан (форс-экран)", () => {
      setup({});
      expect(screen.queryByRole("button", { name: "Отмена" })).not.toBeInTheDocument();
    });
  });
});
