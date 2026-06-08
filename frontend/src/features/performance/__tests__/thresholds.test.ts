import { describe, expect, it } from "vitest";
import {
  cpuSaturation,
  diskFillPercent,
  diskMaxLatencySec,
  diskSaturation,
  ramSaturation,
  ramUsedPercent,
} from "../thresholds";
import type { CpuMetrics, DiskMetrics, MemoryMetrics } from "../types";

function cpu(o: Partial<CpuMetrics>): CpuMetrics {
  return { totalPercent: 0, queueLength: 0, ...o };
}
function mem(o: Partial<MemoryMetrics>): MemoryMetrics {
  return { availableMBytes: 8192, totalMBytes: 16384, pagesPerSec: 0, ...o };
}
function disk(o: Partial<DiskMetrics>): DiskMetrics {
  return { avgReadSecPerOp: 0, avgWriteSecPerOp: 0, queueLength: 0, ...o };
}

describe("cpuSaturation", () => {
  it("ok when percent and queue are low", () => {
    expect(cpuSaturation(cpu({ totalPercent: 40, queueLength: 1 }))).toBe("ok");
  });

  it("warns on percent band even with empty queue", () => {
    expect(cpuSaturation(cpu({ totalPercent: 80, queueLength: 0 }))).toBe("warn");
  });

  it("takes the worse of percent and queue", () => {
    // low percent but a crit-level queue → crit (saturation beats raw utilization)
    expect(cpuSaturation(cpu({ totalPercent: 30, queueLength: 5 }))).toBe("crit");
  });

  it("crit on percent ≥ 90", () => {
    expect(cpuSaturation(cpu({ totalPercent: 95, queueLength: 0 }))).toBe("crit");
  });
});

describe("ramUsedPercent", () => {
  it("computes used share from available/total", () => {
    expect(ramUsedPercent(mem({ availableMBytes: 4096, totalMBytes: 16384 }))).toBe(75);
  });

  it("guards against zero total", () => {
    expect(ramUsedPercent(mem({ totalMBytes: 0 }))).toBe(0);
  });
});

describe("ramSaturation", () => {
  it("ok when usage and paging are low", () => {
    expect(ramSaturation(mem({ availableMBytes: 8192, totalMBytes: 16384, pagesPerSec: 50 }))).toBe(
      "ok"
    );
  });

  it("crit on heavy paging even below the usage band", () => {
    expect(
      ramSaturation(mem({ availableMBytes: 8192, totalMBytes: 16384, pagesPerSec: 1500 }))
    ).toBe("crit");
  });

  it("warns at ≥ 80% used", () => {
    expect(ramSaturation(mem({ availableMBytes: 3000, totalMBytes: 16384, pagesPerSec: 0 }))).toBe(
      "warn"
    );
  });
});

describe("diskSaturation / latency", () => {
  it("takes the worse of read and write latency", () => {
    expect(diskMaxLatencySec(disk({ avgReadSecPerOp: 0.005, avgWriteSecPerOp: 0.03 }))).toBe(0.03);
  });

  it("ok under 10 ms", () => {
    expect(diskSaturation(disk({ avgReadSecPerOp: 0.004, avgWriteSecPerOp: 0.006 }))).toBe("ok");
  });

  it("warns between 10 and 20 ms", () => {
    expect(diskSaturation(disk({ avgWriteSecPerOp: 0.015 }))).toBe("warn");
  });

  it("crit at or above 20 ms", () => {
    expect(diskSaturation(disk({ avgWriteSecPerOp: 0.025 }))).toBe("crit");
  });

  it("scales fill to the crit ceiling and caps at 100", () => {
    expect(diskFillPercent(disk({ avgReadSecPerOp: 0.02 }))).toBe(50); // 20ms of 40ms ceiling
    expect(diskFillPercent(disk({ avgReadSecPerOp: 0.1 }))).toBe(100); // capped
  });
});
