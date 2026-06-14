import { describe, it, expect, vi } from "vitest";
import { render, screen, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import "@/i18n";
import { MissingInfobasesDialog } from "../MissingInfobasesDialog";
import type { MissingInfobase } from "../types";

const ITEM: MissingInfobase = {
  infobaseId: "ib-1",
  tenantName: "Клиент A",
  name: "Призрак",
  clusterInfobaseId: "11111111-1111-1111-1111-111111111111",
};

function makeItem(idx: number): MissingInfobase {
  return {
    infobaseId: `ib-${idx}`,
    tenantName: `Клиент ${String(idx).padStart(3, "0")}`,
    name: `База-${idx}`,
    clusterInfobaseId: `00000000-0000-0000-0000-${String(idx).padStart(12, "0")}`,
  };
}

function setup(items = [ITEM]) {
  const onDelete = vi.fn();
  const onOpenChange = vi.fn();
  render(
    <MissingInfobasesDialog open onOpenChange={onOpenChange} items={items} onDelete={onDelete} />
  );
  return { onDelete, user: userEvent.setup() };
}

describe("MissingInfobasesDialog", () => {
  it("показывает клиента, имя и UUID базы", () => {
    setup();
    expect(screen.getByText("Призрак")).toBeInTheDocument();
    expect(screen.getByText("Клиент A")).toBeInTheDocument();
    expect(screen.getByText(ITEM.clusterInfobaseId)).toBeInTheDocument();
  });

  it("«Удалить» прокидывает запись в delete-флоу", async () => {
    const { onDelete, user } = setup();
    await user.click(screen.getByRole("button", { name: "Удалить" }));
    expect(onDelete).toHaveBeenCalledWith(ITEM);
  });

  it("при <= 20 элементах PaginationBar не рендерится (всё на одной странице)", () => {
    const items = Array.from({ length: 15 }, (_, i) => makeItem(i + 1));
    setup(items);
    // PaginationBar возвращает null при total <= pageSize
    expect(screen.queryByRole("navigation")).not.toBeInTheDocument();
    // Все 15 элементов видны
    expect(screen.getAllByRole("listitem")).toHaveLength(15);
  });

  it("при > 20 элементах показывает только первые 20 и рендерит PaginationBar", () => {
    const items = Array.from({ length: 25 }, (_, i) => makeItem(i + 1));
    setup(items);
    // Первая страница — 20 строк. Скоупим к контентному списку: PaginationBar тоже рендерит
    // <li> (prev/номера/next, role=listitem), поэтому считаем строки только внутри списка баз.
    const list = screen.getByRole("list", { name: "Базы, не найденные в кластере" });
    expect(within(list).getAllByRole("listitem")).toHaveLength(20);
    // PaginationBar присутствует
    expect(screen.getByRole("navigation")).toBeInTheDocument();
  });

  it("элементы отсортированы по клиенту, затем по имени базы", () => {
    const items: MissingInfobase[] = [
      { infobaseId: "x1", tenantName: "Бета", name: "А-база", clusterInfobaseId: "c1" },
      { infobaseId: "x2", tenantName: "Альфа", name: "В-база", clusterInfobaseId: "c2" },
      { infobaseId: "x3", tenantName: "Альфа", name: "А-база", clusterInfobaseId: "c3" },
    ];
    setup(items);
    const listItems = screen.getAllByRole("listitem");
    // Ожидаем порядок: Альфа/А-база, Альфа/В-база, Бета/А-база
    expect(listItems[0]).toHaveTextContent("А-база");
    expect(listItems[0]).toHaveTextContent("Альфа");
    expect(listItems[1]).toHaveTextContent("В-база");
    expect(listItems[2]).toHaveTextContent("Бета");
  });
});
