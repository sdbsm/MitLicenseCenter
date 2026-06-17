import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { describe, expect, it, vi } from "vitest";
import "@/i18n";
import { PublicationsBulkBar } from "../PublicationsBulkBar";

function renderBar(overrides: Partial<Parameters<typeof PublicationsBulkBar>[0]> = {}) {
  const props = {
    count: 3,
    onPublish: vi.fn(),
    onChangePlatform: vi.fn(),
    onCheck: vi.fn(),
    onUnpublish: vi.fn(),
    onDeleteInfobase: vi.fn(),
    onClear: vi.fn(),
    onSelectAllFiltered: vi.fn(),
    ...overrides,
  };
  render(<PublicationsBulkBar {...props} />);
  return props;
}

describe("PublicationsBulkBar (MLC-184b)", () => {
  it("разносит зону выбора и зону действий (justify-between)", () => {
    renderBar();
    // Контейнер бара — flex с justify-between (две зоны разнесены).
    expect(document.body.querySelector(".justify-between")).not.toBeNull();
  });

  it("«Выбрать все по фильтру» — link-кнопка в зоне выбора, не среди действий", () => {
    renderBar();
    const selectAll = screen.getByRole("button", { name: "Выбрать все по фильтру" });
    expect(selectAll).toHaveAttribute("data-variant", "link");
    // В той же зоне — «Снять», но НЕ кнопки-действия (Опубликовать/Сменить платформу/Ещё).
    const selectionZone = selectAll.closest("div");
    expect(selectionZone).not.toBeNull();
    expect(selectionZone!.textContent).toContain("Снять выделение");
    expect(selectionZone!.textContent).not.toContain("Опубликовать выбранные");
    expect(selectionZone!.textContent).not.toContain("Ещё");
  });

  it("«Выбрать все по фильтру» disabled, пока идёт запрос /ids", () => {
    renderBar({ isSelectingAllFiltered: true });
    expect(screen.getByRole("button", { name: "Выбрать все по фильтру" })).toBeDisabled();
  });

  it("меню «Ещё» открывается и содержит ровно три пункта", async () => {
    const user = userEvent.setup();
    renderBar();
    await user.click(screen.getByRole("button", { name: /Ещё/ }));

    expect(
      await screen.findByRole("menuitem", { name: "Проверить публикацию" })
    ).toBeInTheDocument();
    expect(screen.getByRole("menuitem", { name: "Снять с публикации" })).toBeInTheDocument();
    expect(screen.getByRole("menuitem", { name: "Удалить базу" })).toBeInTheDocument();
    expect(screen.getAllByRole("menuitem")).toHaveLength(3);
  });

  it("destructive-разметка у «Снять с публикации» и «Удалить базу», но не у «Проверить»", async () => {
    const user = userEvent.setup();
    renderBar();
    await user.click(screen.getByRole("button", { name: /Ещё/ }));

    const check = await screen.findByRole("menuitem", { name: "Проверить публикацию" });
    const unpublish = screen.getByRole("menuitem", { name: "Снять с публикации" });
    const del = screen.getByRole("menuitem", { name: "Удалить базу" });

    expect(check).toHaveAttribute("data-variant", "default");
    expect(unpublish).toHaveAttribute("data-variant", "destructive");
    expect(del).toHaveAttribute("data-variant", "destructive");
  });

  it("пункты меню вызывают соответствующие хендлеры", async () => {
    const user = userEvent.setup();
    const props = renderBar();

    await user.click(screen.getByRole("button", { name: /Ещё/ }));
    await user.click(await screen.findByRole("menuitem", { name: "Проверить публикацию" }));
    expect(props.onCheck).toHaveBeenCalledTimes(1);

    await user.click(screen.getByRole("button", { name: /Ещё/ }));
    await user.click(await screen.findByRole("menuitem", { name: "Снять с публикации" }));
    expect(props.onUnpublish).toHaveBeenCalledTimes(1);

    await user.click(screen.getByRole("button", { name: /Ещё/ }));
    await user.click(await screen.findByRole("menuitem", { name: "Удалить базу" }));
    expect(props.onDeleteInfobase).toHaveBeenCalledTimes(1);
  });

  it("кнопки-действия зоны действий зовут publish/change-platform", async () => {
    const user = userEvent.setup();
    const props = renderBar();

    await user.click(screen.getByRole("button", { name: "Опубликовать выбранные" }));
    expect(props.onPublish).toHaveBeenCalledTimes(1);

    await user.click(screen.getByRole("button", { name: "Сменить платформу выбранным" }));
    expect(props.onChangePlatform).toHaveBeenCalledTimes(1);
  });
});
