import { describe, it, expect } from "vitest";
import { render, screen } from "@testing-library/react";
import { StatusBadge } from "../StatusBadge";

describe("StatusBadge", () => {
  it("renders children text", () => {
    render(<StatusBadge variant="success">Active</StatusBadge>);
    expect(screen.getByText("Active")).toBeInTheDocument();
  });

  it("applies the success variant Tailwind class", () => {
    render(<StatusBadge variant="success">Active</StatusBadge>);
    const el = screen.getByText("Active");
    expect(el.className).toContain("bg-emerald-500/15");
    expect(el.className).toContain("text-emerald-700");
  });

  it("applies the danger variant Tailwind class", () => {
    render(<StatusBadge variant="danger">Failed</StatusBadge>);
    const el = screen.getByText("Failed");
    expect(el.className).toContain("bg-rose-500/15");
  });

  it("merges a custom className alongside the variant class", () => {
    render(
      <StatusBadge variant="info" className="custom-class">
        Info
      </StatusBadge>,
    );
    const el = screen.getByText("Info");
    expect(el.className).toContain("custom-class");
    expect(el.className).toContain("bg-sky-500/15");
  });
});
