import { describe, it, expect, vi } from "vitest";
import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import "@/i18n";
import { MissingInfobasesBanner } from "../MissingInfobasesBanner";

function setup(count = 1) {
  const onShow = vi.fn();
  render(
    <MissingInfobasesBanner count={count} checkedAtUtc="2026-06-11T10:00:00Z" onShow={onShow} />
  );
  return { onShow, user: userEvent.setup() };
}

describe("MissingInfobasesBanner", () => {
  it("показывает счётчик и свежесть проверки", () => {
    setup(1);
    expect(screen.getByText(/1 база не найдена в кластере 1С/)).toBeInTheDocument();
    expect(screen.getByText("Проверено")).toBeInTheDocument();
  });

  it("«Показать» прокидывает колбэк", async () => {
    const { onShow, user } = setup();
    await user.click(screen.getByRole("button", { name: "Показать" }));
    expect(onShow).toHaveBeenCalled();
  });
});
