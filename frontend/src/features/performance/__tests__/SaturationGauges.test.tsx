import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { describe, expect, it, vi } from "vitest";
import "@/i18n";
import { SaturationGauges } from "../SaturationGauges";
import type { HostMetricsSnapshot } from "../types";

function snapshot(o: Partial<HostMetricsSnapshot> = {}): HostMetricsSnapshot {
  return {
    capturedAtUtc: "2026-06-18T12:00:00Z",
    measuring: false,
    cpu: { totalPercent: 42, queueLength: 1 },
    memory: { availableMBytes: 8192, totalMBytes: 16384, pagesPerSec: 5 },
    disk: { avgReadSecPerOp: 0.002, avgWriteSecPerOp: 0.003, queueLength: 0 },
    processGroups: [],
    processesInaccessible: 0,
    attributionIncomplete: false,
    ...o,
  };
}

describe("SaturationGauges", () => {
  it("renders non-interactive gauges (no buttons) without onResourceClick", () => {
    render(<SaturationGauges snapshot={snapshot()} />);
    expect(screen.queryAllByRole("button")).toHaveLength(0);
  });

  it("renders each gauge as a button and fires onResourceClick with the right resource", async () => {
    const onResourceClick = vi.fn<(resource: "cpu" | "ram" | "disk") => void>();
    render(<SaturationGauges snapshot={snapshot()} onResourceClick={onResourceClick} />);

    const buttons = screen.getAllByRole("button");
    expect(buttons).toHaveLength(3);

    // Кнопки идут в порядке CPU → RAM → Disk.
    await userEvent.click(buttons[0]);
    expect(onResourceClick).toHaveBeenLastCalledWith("cpu");

    await userEvent.click(buttons[1]);
    expect(onResourceClick).toHaveBeenLastCalledWith("ram");

    await userEvent.click(buttons[2]);
    expect(onResourceClick).toHaveBeenLastCalledWith("disk");

    expect(onResourceClick).toHaveBeenCalledTimes(3);
  });
});
