import { describe, it, expect, vi, beforeEach } from "vitest";
import { render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import type { ReactNode } from "react";
import "@/i18n";
import type * as ApiModule from "@/lib/api";
import { ChangeRoleDialog } from "../ChangeRoleDialog";
import type { User } from "../types";
import { usersQueryKey } from "../useUsers";

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

const viewerUser: User = {
  id: "33333333-3333-3333-3333-333333333333",
  userName: "watcher",
  roles: ["Viewer"],
  isActive: true,
  lastLoginAt: null,
};

const adminUser: User = {
  ...viewerUser,
  id: "44444444-4444-4444-4444-444444444444",
  userName: "operator",
  roles: ["Admin"],
};

function setup(user: User) {
  const client = new QueryClient({
    defaultOptions: { queries: { retry: false }, mutations: { retry: false } },
  });
  const invalidateSpy = vi.spyOn(client, "invalidateQueries");
  const onOpenChange = vi.fn();
  const wrapper = ({ children }: { children: ReactNode }) => (
    <QueryClientProvider client={client}>{children}</QueryClientProvider>
  );
  render(<ChangeRoleDialog user={user} open onOpenChange={onOpenChange} />, { wrapper });
  return { invalidateSpy, onOpenChange, u: userEvent.setup() };
}

describe("ChangeRoleDialog", () => {
  beforeEach(() => {
    mockedApi.mockReset();
    mockedToastSuccess.mockReset();
    mockedToastError.mockReset();
  });

  it("повышает Viewer до Admin, инвалидирует кэш и закрывает диалог", async () => {
    mockedApi.mockResolvedValueOnce(null);
    const { invalidateSpy, onOpenChange, u } = setup(viewerUser);

    await u.click(screen.getByRole("radio", { name: /Администратор/ }));
    await u.click(screen.getByRole("button", { name: "Сохранить" }));

    await waitFor(() =>
      expect(mockedApi).toHaveBeenCalledWith(`/api/v1/users/${viewerUser.id}/role`, {
        method: "POST",
        body: { role: "Admin" },
      })
    );
    expect(invalidateSpy).toHaveBeenCalledWith({ queryKey: usersQueryKey });
    expect(mockedToastSuccess).toHaveBeenCalled();
    expect(onOpenChange).toHaveBeenCalledWith(false);
  });

  it("409 USER_LAST_ACTIVE → понятный тост о разжаловании, диалог не закрывается", async () => {
    mockedApi.mockRejectedValueOnce(new ApiError(409, "conflict", { code: "USER_LAST_ACTIVE" }));
    const { onOpenChange, u } = setup(adminUser);

    await u.click(screen.getByRole("radio", { name: /Наблюдатель/ }));
    await u.click(screen.getByRole("button", { name: "Сохранить" }));

    await waitFor(() =>
      expect(mockedToastError).toHaveBeenCalledWith(
        "Нельзя разжаловать последнего активного администратора."
      )
    );
    expect(onOpenChange).not.toHaveBeenCalledWith(false);
  });

  it("409 USER_CANNOT_CHANGE_OWN_ROLE → понятный тост", async () => {
    mockedApi.mockRejectedValueOnce(
      new ApiError(409, "conflict", { code: "USER_CANNOT_CHANGE_OWN_ROLE" })
    );
    const { u } = setup(adminUser);

    await u.click(screen.getByRole("radio", { name: /Наблюдатель/ }));
    await u.click(screen.getByRole("button", { name: "Сохранить" }));

    await waitFor(() =>
      expect(mockedToastError).toHaveBeenCalledWith(
        "Нельзя менять роль собственной учётной записи."
      )
    );
  });
});
