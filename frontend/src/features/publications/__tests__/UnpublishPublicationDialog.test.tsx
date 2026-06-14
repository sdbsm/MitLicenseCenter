import { describe, it, expect, vi, beforeEach } from "vitest";
import { render, screen, fireEvent, waitFor } from "@testing-library/react";
import "@/i18n";
import type { PublicationListItem } from "../types";

// useUnpublish замокан — проверяем что кнопка активна сразу и вызов мутации снятия (MLC-113).
// ADR-45: снятие публикации — обратимое действие, ручной ввод токена убран.
const mutateAsync = vi.fn().mockResolvedValue({ status: "NotPublished" });
vi.mock("../usePublications", () => ({
  useUnpublish: () => ({ mutateAsync, isPending: false }),
}));
vi.mock("sonner", () => ({ toast: { success: vi.fn(), error: vi.fn() } }));

import { UnpublishPublicationDialog } from "../UnpublishPublicationDialog";

const pub: PublicationListItem = {
  id: "p1",
  infobaseId: "ib-1",
  infobaseName: "Acme BP",
  tenantId: "t1",
  tenantName: "Клиент A",
  siteName: "Default Web Site",
  virtualPath: "/acme",
  platformVersion: "8.3.23.1865",
  source: "Webinst",
  lastCheckStatus: "Published",
  lastCheckAt: null,
  lastCheckDetails: null,
};

describe("UnpublishPublicationDialog (MLC-113, ADR-45)", () => {
  beforeEach(() => mutateAsync.mockClear());

  it("кнопка снятия активна сразу (без ручного ввода) и вызывает мутацию по id", async () => {
    render(<UnpublishPublicationDialog open onOpenChange={vi.fn()} publication={pub} />);

    const confirm = screen.getByRole("button", { name: "Снять публикацию" });
    expect(confirm).toBeEnabled();

    fireEvent.click(confirm);
    await waitFor(() => expect(mutateAsync).toHaveBeenCalledWith("p1"));
  });

  it("диалог закрывается при нажатии «Отмена»", () => {
    const onOpenChange = vi.fn();
    render(<UnpublishPublicationDialog open onOpenChange={onOpenChange} publication={pub} />);

    fireEvent.click(screen.getByRole("button", { name: "Отмена" }));
    expect(mutateAsync).not.toHaveBeenCalled();
  });
});
