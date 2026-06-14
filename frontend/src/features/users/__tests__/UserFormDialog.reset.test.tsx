/**
 * FE-07: проверка сброса формы UserFormDialog при повторном открытии.
 *
 * UserFormDialog держит useForm в теле самого компонента, который UsersPage
 * держит смонтированным всегда (только DialogContent скрывается/показывается).
 * Тест доказывает наличие/отсутствие «призрака» введённых данных при повторном
 * открытии диалога.
 */
import { describe, it, expect, vi, beforeEach } from "vitest";
import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { useState } from "react";
import type { ReactNode } from "react";
import "@/i18n";
import type * as ApiModule from "@/lib/api";
import { UserFormDialog } from "../UserFormDialog";

vi.mock("sonner", () => ({
  toast: { success: vi.fn(), error: vi.fn() },
}));

vi.mock("@/lib/api", async (importOriginal) => {
  const actual = await importOriginal<typeof ApiModule>();
  return { ...actual, api: vi.fn() };
});

// Управляемая обёртка — имитирует поведение UsersPage: UserFormDialog смонтирован
// всегда, open переключается кнопками.
function ControlledWrapper() {
  const [open, setOpen] = useState(false);
  return (
    <>
      <button onClick={() => setOpen(true)}>Открыть</button>
      <UserFormDialog open={open} onOpenChange={setOpen} onPasswordGenerated={vi.fn()} />
    </>
  );
}

function setup() {
  const client = new QueryClient({
    defaultOptions: { queries: { retry: false }, mutations: { retry: false } },
  });
  const wrapper = ({ children }: { children: ReactNode }) => (
    <QueryClientProvider client={client}>{children}</QueryClientProvider>
  );
  render(<ControlledWrapper />, { wrapper });
  return { user: userEvent.setup() };
}

describe("UserFormDialog — сброс формы при повторном открытии (FE-07)", () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it("поле userName пустое при повторном открытии после заполнения и закрытия", async () => {
    const { user } = setup();

    // Открываем диалог
    await user.click(screen.getByRole("button", { name: "Открыть" }));

    // Вводим значение в поле «Логин»
    const input = screen.getByRole("textbox");
    await user.type(input, "test-user");
    expect(input).toHaveValue("test-user");

    // Закрываем диалог (Escape)
    await user.keyboard("{Escape}");

    // Открываем снова
    await user.click(screen.getByRole("button", { name: "Открыть" }));

    // Поле должно быть пустым — форма сброшена
    expect(screen.getByRole("textbox")).toHaveValue("");
  });
});
