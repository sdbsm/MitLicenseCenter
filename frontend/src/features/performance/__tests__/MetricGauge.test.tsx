import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { describe, expect, it, vi } from "vitest";
import "@/i18n";
import { MetricGauge } from "../MetricGauge";

describe("MetricGauge", () => {
  it("renders the formatted value when ready", () => {
    render(<MetricGauge label="Процессор" valueText="42 %" fillPercent={42} saturation="ok" />);
    expect(screen.getByText("42 %")).toBeInTheDocument();
  });

  it("shows «измеряю…» and hides the value on the first poll (no zeros as real data)", () => {
    render(
      <MetricGauge label="Процессор" valueText="0 %" fillPercent={0} saturation="ok" measuring />
    );
    expect(screen.getByText("измеряю…")).toBeInTheDocument();
    expect(screen.queryByText("0 %")).not.toBeInTheDocument();
  });

  it("renders a non-interactive container (not a button) without onClick", () => {
    render(<MetricGauge label="Процессор" valueText="42 %" fillPercent={42} saturation="ok" />);
    expect(screen.queryByRole("button")).not.toBeInTheDocument();
  });

  it("renders a button and fires onClick when onClick is provided", async () => {
    const onClick = vi.fn();
    render(
      <MetricGauge
        label="Процессор"
        valueText="42 %"
        fillPercent={42}
        saturation="ok"
        onClick={onClick}
        ariaLabel="Открыть слой"
      />
    );
    const button = screen.getByRole("button", { name: "Открыть слой" });
    await userEvent.click(button);
    expect(onClick).toHaveBeenCalledTimes(1);
  });
});
