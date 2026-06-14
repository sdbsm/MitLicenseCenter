/**
 * FE-08: проверка сброса формы смены пароля в диалоге Topbar при повторном открытии.
 *
 * ChangePasswordForm держит useForm внутри себя и является потомком DialogContent.
 * Radix по умолчанию размонтирует DialogContent при закрытии — то есть форма
 * должна сбрасываться сама при повторном открытии.
 *
 * Тест доказывает корректность поведения (или находит призрака).
 * Оставляем как регрессионную страховку даже при зелёном прогоне.
 */
import { describe, it, expect, vi, beforeEach } from "vitest";
import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { useState } from "react";
import type { ReactNode } from "react";
import "@/i18n";
import type * as ApiModule from "@/lib/api";
import {
  Dialog,
  DialogContent,
  DialogHeader,
  DialogTitle,
  DialogDescription,
} from "@/components/ui/dialog";
import { ChangePasswordForm } from "@/features/profile/ChangePasswordForm";

vi.mock("sonner", () => ({
  toast: { success: vi.fn(), error: vi.fn() },
}));

vi.mock("@/lib/api", async (importOriginal) => {
  const actual = await importOriginal<typeof ApiModule>();
  return { ...actual, api: vi.fn() };
});

// Минимальная обёртка — воспроизводит структуру Topbar: Dialog управляемый, внутри
// DialogContent → ChangePasswordForm. Без навигации и useMe (нам нужна только форма).
function ControlledPasswordDialog() {
  const [open, setOpen] = useState(false);
  return (
    <>
      <button onClick={() => setOpen(true)}>Открыть смену пароля</button>
      <Dialog open={open} onOpenChange={setOpen}>
        <DialogContent className="sm:max-w-md">
          <DialogHeader>
            <DialogTitle>Смена пароля</DialogTitle>
            <DialogDescription>Введите текущий и новый пароль.</DialogDescription>
          </DialogHeader>
          <ChangePasswordForm showReset={false} onSuccess={() => setOpen(false)} />
        </DialogContent>
      </Dialog>
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
  render(<ControlledPasswordDialog />, { wrapper });
  return { user: userEvent.setup() };
}

describe("Topbar — диалог смены пароля сбрасывается при повторном открытии (FE-08)", () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it("поля пароля пустые при повторном открытии после заполнения и закрытия", async () => {
    const { user } = setup();

    // Открываем диалог
    await user.click(screen.getByRole("button", { name: "Открыть смену пароля" }));

    // Заполняем поле «Текущий пароль»
    const currentPasswordInput = screen.getByLabelText("Текущий пароль");
    await user.type(currentPasswordInput, "OldPassword#123");
    expect(currentPasswordInput).toHaveValue("OldPassword#123");

    // Закрываем диалог (Escape — Radix должен размонтировать DialogContent)
    await user.keyboard("{Escape}");

    // Убеждаемся что диалог закрыт
    expect(screen.queryByLabelText("Текущий пароль")).not.toBeInTheDocument();

    // Открываем снова
    await user.click(screen.getByRole("button", { name: "Открыть смену пароля" }));

    // Поле должно быть пустым — ChangePasswordForm перемонтировалась заново
    expect(screen.getByLabelText("Текущий пароль")).toHaveValue("");
  });
});
