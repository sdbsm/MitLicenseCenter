import { ThemeProvider } from "next-themes";
import { afterEach, describe, expect, it } from "vitest";
import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import "@/i18n";
import { ThemeToggle } from "../ThemeToggle";

function renderToggle() {
  return render(
    <ThemeProvider attribute="class" defaultTheme="system" enableSystem storageKey="mlc-theme">
      <ThemeToggle />
    </ThemeProvider>
  );
}

afterEach(() => {
  // next-themes пишет выбор в localStorage и класс на <html> — сбрасываем,
  // чтобы тесты не протекали друг в друга.
  localStorage.clear();
  document.documentElement.classList.remove("dark", "light");
});

describe("ThemeToggle", () => {
  it("рендерит триггер с подписью темы", () => {
    renderToggle();
    expect(screen.getByRole("button", { name: "Тема" })).toBeInTheDocument();
  });

  it("выбор «Тёмная» вешает класс .dark на <html> и сохраняет выбор", async () => {
    const user = userEvent.setup();
    renderToggle();

    await user.click(screen.getByRole("button", { name: "Тема" }));
    await user.click(screen.getByRole("menuitemradio", { name: "Тёмная" }));

    expect(document.documentElement).toHaveClass("dark");
    expect(localStorage.getItem("mlc-theme")).toBe("dark");
  });

  it("выбор «Светлая» снимает класс .dark", async () => {
    const user = userEvent.setup();
    renderToggle();

    await user.click(screen.getByRole("button", { name: "Тема" }));
    await user.click(screen.getByRole("menuitemradio", { name: "Светлая" }));

    expect(document.documentElement).not.toHaveClass("dark");
    expect(localStorage.getItem("mlc-theme")).toBe("light");
  });
});
