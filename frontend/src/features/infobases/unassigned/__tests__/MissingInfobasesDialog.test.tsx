import { describe, it, expect, vi } from "vitest";
import { render, screen } from "@testing-library/react";
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

function setup() {
  const onDelete = vi.fn();
  const onOpenChange = vi.fn();
  render(
    <MissingInfobasesDialog open onOpenChange={onOpenChange} items={[ITEM]} onDelete={onDelete} />
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
});
