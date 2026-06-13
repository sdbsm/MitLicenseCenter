import { describe, it, expect } from "vitest";
import { render, screen } from "@testing-library/react";
import { StatusBadge, type StatusBadgeVariant } from "../StatusBadge";

// FE-19 (MLC-120): поведенческие ассерты вместо проверки Tailwind-классов
// (`bg-emerald-500/15` и т.п. давали ложно-красный при дизайн-рефакторе). Наблюдаемый
// стабильный контракт: рендерится children-текст и выбранный вариант отражён в
// data-variant (status→variant маппинг), а кастомный className по-прежнему доезжает.
describe("StatusBadge", () => {
  it("рендерит children-текст", () => {
    render(<StatusBadge variant="success">Active</StatusBadge>);
    expect(screen.getByText("Active")).toBeInTheDocument();
  });

  const variants: StatusBadgeVariant[] = ["success", "warning", "danger", "info", "neutral"];

  it.each(variants)("отражает вариант '%s' через data-variant", (variant) => {
    render(<StatusBadge variant={variant}>Label</StatusBadge>);
    const el = screen.getByText("Label");
    expect(el).toHaveAttribute("data-variant", variant);
  });

  it("прокидывает кастомный className рядом с вариантом", () => {
    render(
      <StatusBadge variant="info" className="custom-class">
        Info
      </StatusBadge>,
    );
    const el = screen.getByText("Info");
    expect(el).toHaveAttribute("data-variant", "info");
    expect(el.className).toContain("custom-class");
  });
});
