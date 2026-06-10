import { describe, it, expect, vi } from "vitest";
import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import "@/i18n";
import { UnassignedBanner } from "../UnassignedBanner";

function setup(count = 3) {
  const onRefresh = vi.fn();
  const onResolve = vi.fn();
  render(
    <UnassignedBanner
      count={count}
      checkedAtUtc="2026-06-11T10:00:00Z"
      onRefresh={onRefresh}
      onResolve={onResolve}
      isRefreshing={false}
    />
  );
  return { onRefresh, onResolve, user: userEvent.setup() };
}

describe("UnassignedBanner", () => {
  it("показывает счётчик и индикатор свежести", () => {
    setup(3);
    expect(screen.getByText(/3 базы кластера не заведены/)).toBeInTheDocument();
    expect(screen.getByText("Проверено")).toBeInTheDocument();
  });

  it("«Разобрать» и «Обновить» прокидывают колбэки", async () => {
    const { onRefresh, onResolve, user } = setup();
    await user.click(screen.getByRole("button", { name: "Разобрать" }));
    expect(onResolve).toHaveBeenCalled();
    await user.click(screen.getByRole("button", { name: "Обновить" }));
    expect(onRefresh).toHaveBeenCalled();
  });
});
