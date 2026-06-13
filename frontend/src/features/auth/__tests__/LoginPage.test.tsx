import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { MemoryRouter } from "react-router";
import type * as ReactRouterModule from "react-router";
import type { ReactNode } from "react";
import { beforeEach, describe, expect, it, vi } from "vitest";
import "@/i18n";
import { ApiError } from "@/lib/api";
import type * as UseAuthModule from "../useAuth";
import { LoginPage } from "../LoginPage";

const mutateAsync = vi.fn();
const navigate = vi.fn();
const toastSuccess = vi.fn();
const toastError = vi.fn();

vi.mock("sonner", () => ({
  toast: {
    success: (...args: unknown[]) => toastSuccess(...args),
    error: (...args: unknown[]) => toastError(...args),
  },
}));

vi.mock("react-router", async (importOriginal) => {
  const actual = await importOriginal<typeof ReactRouterModule>();
  return { ...actual, useNavigate: () => navigate };
});

// useLogin мокаем: форма сама прогоняет Zod-валидацию (userName regex / required) ДО
// вызова login — именно эту валидацию и тестируем (FE-11, MLC-120).
vi.mock("../useAuth", async (importOriginal) => {
  const actual = await importOriginal<typeof UseAuthModule>();
  return {
    ...actual,
    useLogin: () => ({ mutateAsync, isPending: false }),
  };
});

function setup() {
  const client = new QueryClient({
    defaultOptions: { queries: { retry: false }, mutations: { retry: false } },
  });
  const wrapper = ({ children }: { children: ReactNode }) => (
    <QueryClientProvider client={client}>
      <MemoryRouter>{children}</MemoryRouter>
    </QueryClientProvider>
  );
  render(<LoginPage />, { wrapper });
  return userEvent.setup();
}

describe("LoginPage", () => {
  beforeEach(() => {
    mutateAsync.mockReset();
    navigate.mockReset();
    toastSuccess.mockReset();
    toastError.mockReset();
  });

  it("валидный вход вызывает login и навигирует на /", async () => {
    mutateAsync.mockResolvedValueOnce({ userName: "admin" });
    const u = setup();

    await u.type(screen.getByLabelText("Имя пользователя"), "admin");
    await u.type(screen.getByLabelText("Пароль"), "secret");
    await u.click(screen.getByRole("button", { name: "Войти" }));

    await waitFor(() =>
      expect(mutateAsync).toHaveBeenCalledWith({ userName: "admin", password: "secret" })
    );
    expect(navigate).toHaveBeenCalledWith("/", { replace: true });
    expect(toastSuccess).toHaveBeenCalled();
  });

  it("отклоняет userName с недопустимым символом (пробел) — login НЕ вызывается", async () => {
    const u = setup();

    await u.type(screen.getByLabelText("Имя пользователя"), "ad min");
    await u.type(screen.getByLabelText("Пароль"), "secret");
    await u.click(screen.getByRole("button", { name: "Войти" }));

    // Zod-схема формы (regex /^[a-zA-Z0-9\-._@+]+$/) роняет валидацию: сабмит не уходит.
    await waitFor(() => expect(screen.getByText("Укажите имя пользователя.")).toBeInTheDocument());
    expect(mutateAsync).not.toHaveBeenCalled();
  });

  it("отклоняет пустой пароль — login НЕ вызывается", async () => {
    const u = setup();

    await u.type(screen.getByLabelText("Имя пользователя"), "admin");
    await u.click(screen.getByRole("button", { name: "Войти" }));

    await waitFor(() => expect(screen.getByText("Укажите пароль.")).toBeInTheDocument());
    expect(mutateAsync).not.toHaveBeenCalled();
  });

  it("показывает «неверные учётные данные» при 401", async () => {
    mutateAsync.mockRejectedValueOnce(new ApiError(401, "HTTP 401", null));
    const u = setup();

    await u.type(screen.getByLabelText("Имя пользователя"), "admin");
    await u.type(screen.getByLabelText("Пароль"), "wrong");
    await u.click(screen.getByRole("button", { name: "Войти" }));

    await waitFor(() =>
      expect(toastError).toHaveBeenCalledWith("Неверное имя пользователя или пароль.")
    );
    expect(navigate).not.toHaveBeenCalled();
  });

  it("показывает общую ошибку при не-401 сбое", async () => {
    mutateAsync.mockRejectedValueOnce(new Error("network down"));
    const u = setup();

    await u.type(screen.getByLabelText("Имя пользователя"), "admin");
    await u.type(screen.getByLabelText("Пароль"), "secret");
    await u.click(screen.getByRole("button", { name: "Войти" }));

    await waitFor(() =>
      expect(toastError).toHaveBeenCalledWith("Произошла ошибка. Попробуйте ещё раз.")
    );
  });
});
