import { describe, it, expect, vi, beforeEach } from "vitest";
import { render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { MemoryRouter } from "react-router";
import type { ReactNode } from "react";
import "@/i18n";
import type * as ApiModule from "@/lib/api";
import type * as UseAuthModule from "../useAuth";
import { ForcePasswordChange } from "../ForcePasswordChange";
import { ME_KEY } from "../useAuth";

vi.mock("sonner", () => ({
  toast: { success: vi.fn(), error: vi.fn() },
}));

vi.mock("@/lib/api", async (importOriginal) => {
  const actual = await importOriginal<typeof ApiModule>();
  return { ...actual, api: vi.fn() };
});

// useLogout зовётся только по кнопке «Выйти», которую happy-path не нажимает;
// мокаем его, чтобы экран рендерился без сетевого мутатора.
vi.mock("../useAuth", async (importOriginal) => {
  const actual = await importOriginal<typeof UseAuthModule>();
  return { ...actual, useLogout: () => ({ mutateAsync: vi.fn() }) };
});

import { api } from "@/lib/api";

const mockedApi = vi.mocked(api);

// Пароль ≥12 символов, удовлетворяющий zod-схеме формы смены пароля.
const NEW_PASSWORD = "NewStrongPass#1";

function setup() {
  const client = new QueryClient({
    defaultOptions: { queries: { retry: false }, mutations: { retry: false } },
  });
  const invalidateSpy = vi.spyOn(client, "invalidateQueries");
  const wrapper = ({ children }: { children: ReactNode }) => (
    <QueryClientProvider client={client}>
      <MemoryRouter>{children}</MemoryRouter>
    </QueryClientProvider>
  );
  render(<ForcePasswordChange />, { wrapper });
  return { invalidateSpy, u: userEvent.setup() };
}

describe("ForcePasswordChange", () => {
  beforeEach(() => {
    mockedApi.mockReset();
  });

  it("успешная смена пароля инвалидирует /me — блокирующий экран снимается", async () => {
    mockedApi.mockResolvedValueOnce(null);
    const { invalidateSpy, u } = setup();

    await u.type(screen.getByLabelText("Текущий пароль"), "OldPass#123456");
    await u.type(screen.getByLabelText("Новый пароль"), NEW_PASSWORD);
    await u.type(screen.getByLabelText("Повторите новый пароль"), NEW_PASSWORD);
    await u.click(screen.getByRole("button", { name: "Сменить и продолжить" }));

    await waitFor(() =>
      expect(mockedApi).toHaveBeenCalledWith("/api/v1/auth/change-password", {
        method: "POST",
        body: { currentPassword: "OldPass#123456", newPassword: NEW_PASSWORD },
      })
    );
    expect(invalidateSpy).toHaveBeenCalledWith({ queryKey: ME_KEY });
  });
});
