import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { describe, expect, it, vi } from "vitest";
import "@/i18n";

// Замокать тяжёлые слои — их live-хуки (useOneCLoad/useSqlPerformance) в тест не тянем.
vi.mock("../OneCLoadSection", () => ({ OneCLoadSection: () => null }));
vi.mock("../SqlLoadSection", () => ({ SqlLoadSection: () => null }));

import { PerformanceDrillDown } from "../PerformanceDrillDown";

function renderDrillDown(
  overrides: Partial<React.ComponentProps<typeof PerformanceDrillDown>> = {}
) {
  const onLayerChange = vi.fn();
  render(
    <PerformanceDrillDown
      layer="host"
      onLayerChange={onLayerChange}
      families={[]}
      measuring={false}
      paused={false}
      {...overrides}
    />
  );
  return { onLayerChange };
}

describe("PerformanceDrillDown", () => {
  it("renders three layer tabs: Хост / 1С / SQL", () => {
    renderDrillDown();
    expect(screen.getByRole("tab", { name: "Хост" })).toBeInTheDocument();
    expect(screen.getByRole("tab", { name: "1С" })).toBeInTheDocument();
    expect(screen.getByRole("tab", { name: "SQL" })).toBeInTheDocument();
  });

  it("marks the controlled layer as the active tab", () => {
    renderDrillDown({ layer: "sql" });
    expect(screen.getByRole("tab", { name: "SQL" })).toHaveAttribute("data-state", "active");
    expect(screen.getByRole("tab", { name: "Хост" })).toHaveAttribute("data-state", "inactive");
  });

  it("calls onLayerChange with the clicked layer key", async () => {
    const user = userEvent.setup();
    const { onLayerChange } = renderDrillDown({ layer: "host" });

    await user.click(screen.getByRole("tab", { name: "1С" }));
    expect(onLayerChange).toHaveBeenCalledWith("onec");
  });
});
