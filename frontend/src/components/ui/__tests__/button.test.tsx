import { render, screen } from "@testing-library/react";
import { describe, expect, it } from "vitest";
import { Button } from "../button";

// Вариант `success` (MLC-204) централизует зелёное действие «запуск/старт» (старт IIS-
// сайта/пула/сервера) в слое UI-примитива — пара к `destructive` для «стоп». Раньше
// зелёный задавался сырым `bg-emerald-600` прямо в экранах IIS; теперь — через вариант,
// чтобы в features не было сырых цветовых классов.
describe("Button — вариант success", () => {
  it("прокидывает data-variant и сплошной зелёный с белым текстом (контраст AA)", () => {
    render(<Button variant="success">Запустить</Button>);
    const button = screen.getByRole("button", { name: "Запустить" });
    expect(button).toHaveAttribute("data-variant", "success");
    expect(button.className).toContain("bg-emerald-600");
    expect(button.className).toContain("text-white");
  });

  it("по умолчанию вариант default (success не протекает в прочие кнопки)", () => {
    render(<Button>Обычная</Button>);
    expect(screen.getByRole("button", { name: "Обычная" })).toHaveAttribute(
      "data-variant",
      "default"
    );
  });
});
