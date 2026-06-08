import { render, screen } from "@testing-library/react";
import { describe, expect, it } from "vitest";
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
});
