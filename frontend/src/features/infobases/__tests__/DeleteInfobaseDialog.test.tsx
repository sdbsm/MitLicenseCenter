import { describe, it, expect, vi, beforeEach } from "vitest";
import { render, screen, fireEvent, waitFor } from "@testing-library/react";
import type { ReactNode } from "react";
import "@/i18n";

// useDeleteInfobase замокан — проверяем, с какими аргументами вызывается мутация
// удаления (id + опциональный unpublishFromIis) в зависимости от чекбокса MLC-113.
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

async function confirmDelete(name: string) {
  const input = screen.getByPlaceholderText(name);
  fireEvent.change(input, { target: { value: name } });
  fireEvent.click(screen.getByRole("button", { name: "Удалить" }));
  await waitFor(() => expect(mutateAsync).toHaveBeenCalled());
}

describe("DeleteInfobaseDialog — опция снятия публикации из IIS (MLC-113)", () => {
  beforeEach(() => {
    mutateAsync.mockClear();
  });

  it("publishStatus=Published → чекбокс отмечен, удаление с unpublishFromIis: true", async () => {
    renderDialog({ id: "ib-1", name: "Acme BP", publishStatus: "Published" });

    const checkbox = screen.getByRole("checkbox");
    expect(checkbox).toBeChecked();

    await confirmDelete("Acme BP");
    expect(mutateAsync).toHaveBeenCalledWith({ id: "ib-1", unpublishFromIis: true });
  });

  it("снятие галочки → удаление с unpublishFromIis: false", async () => {
    renderDialog({ id: "ib-1", name: "Acme BP", publishStatus: "Published" });

    fireEvent.click(screen.getByRole("checkbox"));
    expect(screen.getByRole("checkbox")).not.toBeChecked();

    await confirmDelete("Acme BP");
    expect(mutateAsync).toHaveBeenCalledWith({ id: "ib-1", unpublishFromIis: false });
  });

  it("publishStatus=NotPublished → чекбокс по умолчанию снят", async () => {
    renderDialog({ id: "ib-1", name: "Acme BP", publishStatus: "NotPublished" });

    expect(screen.getByRole("checkbox")).not.toBeChecked();

    await confirmDelete("Acme BP");
    expect(mutateAsync).toHaveBeenCalledWith({ id: "ib-1", unpublishFromIis: false });
  });

  it("publishStatus не передан (обратный дрейф) → чекбокса нет, удаление без флага", async () => {
    renderDialog({ id: "ib-1", name: "Acme BP" });

    expect(screen.queryByRole("checkbox")).not.toBeInTheDocument();

    await confirmDelete("Acme BP");
    expect(mutateAsync).toHaveBeenCalledWith({ id: "ib-1", unpublishFromIis: undefined });
  });
});
