import { describe, it, expect, vi, beforeEach } from "vitest";
import { render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import type { ReactNode } from "react";
import "@/i18n";
import type * as ApiModule from "@/lib/api";
import { DisableUserDialog } from "../DisableUserDialog";
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
  id: "22222222-2222-2222-2222-222222222222",
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
  render(<DisableUserDialog user={sampleUser} open onOpenChange={onOpenChange} />, { wrapper });
  return { onOpenChange, user: userEvent.setup() };
}

describe("DisableUserDialog", () => {
  beforeEach(() => {
    mockedApi.mockReset();
    mockedToastSuccess.mockReset();
    mockedToastError.mockReset();
  });

  it("отключает учётку и закрывает диалог", async () => {
    mockedApi.mockResolvedValueOnce(null);
    const { onOpenChange, user } = setup();

    await user.click(screen.getByRole("button", { name: "Отключить" }));

    await waitFor(() =>
      expect(mockedApi).toHaveBeenCalledWith(`/api/v1/users/${sampleUser.id}/disable`, {
        method: "POST",
      })
    );
    expect(mockedToastSuccess).toHaveBeenCalled();
    expect(onOpenChange).toHaveBeenCalledWith(false);
  });

  it("409 USER_LAST_ACTIVE → понятный тост, диалог не закрывается", async () => {
    mockedApi.mockRejectedValueOnce(new ApiError(409, "conflict", { code: "USER_LAST_ACTIVE" }));
    const { onOpenChange, user } = setup();

    await user.click(screen.getByRole("button", { name: "Отключить" }));

    await waitFor(() =>
      expect(mockedToastError).toHaveBeenCalledWith(
        "Нельзя отключить последнего активного администратора."
      )
    );
    expect(onOpenChange).not.toHaveBeenCalledWith(false);
  });

  it("409 USER_CANNOT_DISABLE_SELF → понятный тост", async () => {
    mockedApi.mockRejectedValueOnce(
      new ApiError(409, "conflict", { code: "USER_CANNOT_DISABLE_SELF" })
    );
    const { user } = setup();

    await user.click(screen.getByRole("button", { name: "Отключить" }));

    await waitFor(() =>
      expect(mockedToastError).toHaveBeenCalledWith("Нельзя отключить собственную учётную запись.")
    );
  });
});
