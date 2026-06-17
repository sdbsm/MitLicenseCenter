import { describe, it, expect, vi, beforeEach } from "vitest";
import { render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import type { ReactNode } from "react";
import "@/i18n";
import type * as ApiModule from "@/lib/api";
import { DeleteUserDialog } from "../DeleteUserDialog";
import type { User } from "../types";

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

const sampleUser: User = {
  id: "33333333-3333-3333-3333-333333333333",
  userName: "operator",
  roles: ["Admin"],
  isActive: true,
  lastLoginAt: null,
};

function setup() {
  const client = new QueryClient({
    defaultOptions: { queries: { retry: false }, mutations: { retry: false } },
  });
  const onOpenChange = vi.fn();
  const wrapper = ({ children }: { children: ReactNode }) => (
    <QueryClientProvider client={client}>{children}</QueryClientProvider>
  );
  render(<DeleteUserDialog user={sampleUser} open onOpenChange={onOpenChange} />, { wrapper });
  return { onOpenChange, user: userEvent.setup() };
}

describe("DeleteUserDialog", () => {
  beforeEach(() => {
    mockedApi.mockReset();
    mockedToastSuccess.mockReset();
    mockedToastError.mockReset();
  });

  it("показывает предупреждение о необратимости", () => {
    setup();
    expect(
      screen.getByText("Действие необратимо: учётная запись будет удалена навсегда.")
    ).toBeInTheDocument();
  });

  it("удаляет учётку (DELETE), показывает тост успеха и закрывает диалог", async () => {
    mockedApi.mockResolvedValueOnce(null);
    const { onOpenChange, user } = setup();

    await user.click(screen.getByRole("button", { name: "Удалить" }));

    await waitFor(() =>
      expect(mockedApi).toHaveBeenCalledWith(`/api/v1/users/${sampleUser.id}`, {
        method: "DELETE",
      })
    );
    expect(mockedToastSuccess).toHaveBeenCalled();
    expect(onOpenChange).toHaveBeenCalledWith(false);
  });

  it("409 USER_LAST_ACTIVE → delete-специфичный тост, диалог не закрывается", async () => {
    mockedApi.mockRejectedValueOnce(new ApiError(409, "conflict", { code: "USER_LAST_ACTIVE" }));
    const { onOpenChange, user } = setup();

    await user.click(screen.getByRole("button", { name: "Удалить" }));

    await waitFor(() =>
      expect(mockedToastError).toHaveBeenCalledWith(
        "Нельзя удалить последнего активного администратора."
      )
    );
    expect(onOpenChange).not.toHaveBeenCalledWith(false);
  });

  it("409 USER_CANNOT_DISABLE_SELF → delete-специфичный тост", async () => {
    mockedApi.mockRejectedValueOnce(
      new ApiError(409, "conflict", { code: "USER_CANNOT_DISABLE_SELF" })
    );
    const { user } = setup();

    await user.click(screen.getByRole("button", { name: "Удалить" }));

    await waitFor(() =>
      expect(mockedToastError).toHaveBeenCalledWith("Нельзя удалить собственную учётную запись.")
    );
  });
});
