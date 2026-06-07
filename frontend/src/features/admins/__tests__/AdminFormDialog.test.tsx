import { describe, it, expect, vi, beforeEach } from "vitest";
import { render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import type { ReactNode } from "react";
import "@/i18n";
import type * as ApiModule from "@/lib/api";
import { AdminFormDialog } from "../AdminFormDialog";
import { adminsQueryKey } from "../useAdmins";

vi.mock("sonner", () => ({
  toast: { success: vi.fn(), error: vi.fn() },
}));

// Частичный мок: настоящий ApiError (диалог делает instanceof), мок только сетевого api.
vi.mock("@/lib/api", async (importOriginal) => {
  const actual = await importOriginal<typeof ApiModule>();
  return { ...actual, api: vi.fn() };
});

import { api, ApiError } from "@/lib/api";
import { toast } from "sonner";

const mockedApi = vi.mocked(api);
const mockedToastError = vi.mocked(toast.error);

function setup() {
  const client = new QueryClient({
    defaultOptions: { queries: { retry: false }, mutations: { retry: false } },
  });
  const invalidateSpy = vi.spyOn(client, "invalidateQueries");
  const onOpenChange = vi.fn();
  const onPasswordGenerated = vi.fn();
  const wrapper = ({ children }: { children: ReactNode }) => (
    <QueryClientProvider client={client}>{children}</QueryClientProvider>
  );
  render(
    <AdminFormDialog open onOpenChange={onOpenChange} onPasswordGenerated={onPasswordGenerated} />,
    { wrapper }
  );
  return { invalidateSpy, onOpenChange, onPasswordGenerated, user: userEvent.setup() };
}

describe("AdminFormDialog", () => {
  beforeEach(() => {
    mockedApi.mockReset();
  });

  it("создаёт администратора, инвалидирует кэш и поднимает сгенерированный пароль", async () => {
    mockedApi.mockResolvedValueOnce({
      id: "a1",
      userName: "ivanov",
      generatedPassword: "Aa1!_temp_pwd",
    });
    const { invalidateSpy, onOpenChange, onPasswordGenerated, user } = setup();

    await user.type(screen.getByRole("textbox"), "ivanov");
    await user.click(screen.getByRole("button", { name: "Создать" }));

    await waitFor(() =>
      expect(mockedApi).toHaveBeenCalledWith("/api/v1/admins", {
        method: "POST",
        body: { userName: "ivanov", role: "Admin" },
      })
    );
    expect(invalidateSpy).toHaveBeenCalledWith({ queryKey: adminsQueryKey });
    expect(onOpenChange).toHaveBeenCalledWith(false);
    expect(onPasswordGenerated).toHaveBeenCalledWith("ivanov", "Aa1!_temp_pwd");
  });

  it("роль Viewer уходит в теле запроса при выборе радио", async () => {
    mockedApi.mockResolvedValueOnce({ id: "a2", userName: "watcher", generatedPassword: "pwd" });
    const { user } = setup();

    await user.type(screen.getByRole("textbox"), "watcher");
    await user.click(screen.getByRole("radio", { name: /Наблюдатель/ }));
    await user.click(screen.getByRole("button", { name: "Создать" }));

    await waitFor(() =>
      expect(mockedApi).toHaveBeenCalledWith("/api/v1/admins", {
        method: "POST",
        body: { userName: "watcher", role: "Viewer" },
      })
    );
  });

  it("409 ADMIN_USERNAME_DUPLICATE → ошибка на поле «Логин», диалог открыт", async () => {
    mockedApi.mockRejectedValueOnce(
      new ApiError(409, "conflict", { code: "ADMIN_USERNAME_DUPLICATE" })
    );
    const { onOpenChange, user } = setup();

    await user.type(screen.getByRole("textbox"), "ivanov");
    await user.click(screen.getByRole("button", { name: "Создать" }));

    expect(
      await screen.findByText("Учётная запись с таким логином уже существует.")
    ).toBeInTheDocument();
    expect(onOpenChange).not.toHaveBeenCalled();
    expect(mockedToastError).not.toHaveBeenCalled();
  });
});
