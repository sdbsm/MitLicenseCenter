import { describe, it, expect, vi, beforeEach } from "vitest";
import { render, screen, fireEvent, waitFor } from "@testing-library/react";
import type { ReactNode } from "react";
import "@/i18n";

// useDeleteInfobase замокан — проверяем, с какими аргументами вызывается мутация
// удаления (id + опциональный unpublishFromIis) в зависимости от чекбокса MLC-113.
// ADR-45: необратимое действие — «да/нет» с предупреждением; ручной ввод имени убран.
const mutateAsync = vi.fn().mockResolvedValue(null);
vi.mock("../useInfobases", () => ({
  useDeleteInfobase: () => ({ mutateAsync, isPending: false }),
}));

// sonner — тосты нам не важны.
vi.mock("sonner", () => ({ toast: { success: vi.fn(), error: vi.fn() } }));

import { DeleteInfobaseDialog, type DeletableInfobase } from "../DeleteInfobaseDialog";

function wrapper({ children }: { children: ReactNode }) {
  return <>{children}</>;
}

function renderDialog(infobase: DeletableInfobase) {
  render(<DeleteInfobaseDialog open onOpenChange={vi.fn()} infobase={infobase} />, { wrapper });
}

async function confirmDelete() {
  fireEvent.click(screen.getByRole("button", { name: "Удалить" }));
  await waitFor(() => expect(mutateAsync).toHaveBeenCalled());
}

describe("DeleteInfobaseDialog — опция снятия публикации из IIS (MLC-113, ADR-45)", () => {
  beforeEach(() => {
    mutateAsync.mockClear();
  });

  it("кнопка «Удалить» активна сразу (без ручного ввода)", () => {
    renderDialog({ id: "ib-1", name: "Acme BP", publishStatus: "Published" });
    expect(screen.getByRole("button", { name: "Удалить" })).toBeEnabled();
  });

  it("отображается предупреждение о необратимости", () => {
    renderDialog({ id: "ib-1", name: "Acme BP", publishStatus: "Published" });
    expect(screen.getByText(/необратимо/i)).toBeInTheDocument();
  });

  it("publishStatus=Published → чекбокс отмечен, удаление с unpublishFromIis: true", async () => {
    renderDialog({ id: "ib-1", name: "Acme BP", publishStatus: "Published" });

    const checkbox = screen.getByRole("checkbox");
    expect(checkbox).toBeChecked();

    await confirmDelete();
    expect(mutateAsync).toHaveBeenCalledWith({ id: "ib-1", unpublishFromIis: true });
  });

  it("снятие галочки → удаление с unpublishFromIis: false", async () => {
    renderDialog({ id: "ib-1", name: "Acme BP", publishStatus: "Published" });

    fireEvent.click(screen.getByRole("checkbox"));
    expect(screen.getByRole("checkbox")).not.toBeChecked();

    await confirmDelete();
    expect(mutateAsync).toHaveBeenCalledWith({ id: "ib-1", unpublishFromIis: false });
  });

  it("publishStatus=NotPublished → чекбокс по умолчанию снят", async () => {
    renderDialog({ id: "ib-1", name: "Acme BP", publishStatus: "NotPublished" });

    expect(screen.getByRole("checkbox")).not.toBeChecked();

    await confirmDelete();
    expect(mutateAsync).toHaveBeenCalledWith({ id: "ib-1", unpublishFromIis: false });
  });

  it("publishStatus не передан (обратный дрейф) → чекбокса нет, удаление без флага", async () => {
    renderDialog({ id: "ib-1", name: "Acme BP" });

    expect(screen.queryByRole("checkbox")).not.toBeInTheDocument();

    await confirmDelete();
    expect(mutateAsync).toHaveBeenCalledWith({ id: "ib-1", unpublishFromIis: undefined });
  });
});
