import { describe, it, expect, vi, beforeEach } from "vitest";
import { render, screen, fireEvent, waitFor } from "@testing-library/react";
import "@/i18n";
import type { PublicationListItem } from "../types";

// useUnpublish замокан — проверяем подтверждение токеном и вызов мутации снятия (MLC-113).
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

describe("UnpublishPublicationDialog (MLC-113)", () => {
  beforeEach(() => mutateAsync.mockClear());

  it("кнопка снятия активна только после ввода токена и вызывает мутацию по id", async () => {
    render(<UnpublishPublicationDialog open onOpenChange={vi.fn()} publication={pub} />);

    const confirm = screen.getByRole("button", { name: "Снять публикацию" });
    expect(confirm).toBeDisabled();

    const token = "Default Web Site/acme";
    fireEvent.change(screen.getByPlaceholderText(token), { target: { value: token } });
    expect(confirm).toBeEnabled();

    fireEvent.click(confirm);
    await waitFor(() => expect(mutateAsync).toHaveBeenCalledWith("p1"));
  });

  it("неверный токен оставляет кнопку задизейбленной", () => {
    render(<UnpublishPublicationDialog open onOpenChange={vi.fn()} publication={pub} />);
    fireEvent.change(screen.getByPlaceholderText("Default Web Site/acme"), {
      target: { value: "wrong" },
    });
    expect(screen.getByRole("button", { name: "Снять публикацию" })).toBeDisabled();
  });
});
