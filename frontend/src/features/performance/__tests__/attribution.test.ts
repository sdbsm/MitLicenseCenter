import { describe, expect, it } from "vitest";
import { computeVerdict, dominantFamily, toFamilyShares } from "../attribution";
import type { HostMetricsSnapshot, ProcessGroupUsage } from "../types";

function group(o: Partial<ProcessGroupUsage>): ProcessGroupUsage {
  return { family: "Other", cpuPercent: 0, ramBytes: 0, processCount: 1, ...o };
}

function snapshot(o: Partial<HostMetricsSnapshot>): HostMetricsSnapshot {
  return {
    capturedAtUtc: "2026-06-08T12:00:00Z",
    measuring: false,
    cpu: { totalPercent: 10, queueLength: 0 },
    memory: { availableMBytes: 8192, totalMBytes: 16384, pagesPerSec: 0 },
    disk: { avgReadSecPerOp: 0.002, avgWriteSecPerOp: 0.003, queueLength: 0 },
    processGroups: [],
    ...o,
  };
}

describe("toFamilyShares", () => {
  it("computes cpu/ram shares and sorts by known family order", () => {
    const shares = toFamilyShares([
      group({ family: "Mssql", cpuPercent: 30, ramBytes: 1000 }),
      group({ family: "OneC", cpuPercent: 70, ramBytes: 3000 }),
    ]);
    // sorted: OneC before Mssql
    expect(shares.map((s) => s.family)).toEqual(["OneC", "Mssql"]);
    expect(shares[0].cpuShare).toBeCloseTo(0.7);
    expect(shares[0].ramShare).toBeCloseTo(0.75);
  });

  it("puts unknown family keys after the known ones", () => {
    const shares = toFamilyShares([
      group({ family: "Weird", cpuPercent: 10 }),
      group({ family: "OneC", cpuPercent: 10 }),
    ]);
    expect(shares.map((s) => s.family)).toEqual(["OneC", "Weird"]);
  });

  it("yields zero shares when totals are zero (no divide-by-zero)", () => {
    const shares = toFamilyShares([group({ family: "OneC", cpuPercent: 0, ramBytes: 0 })]);
    expect(shares[0].cpuShare).toBe(0);
    expect(shares[0].ramShare).toBe(0);
  });
});

describe("dominantFamily", () => {
  it("returns the top consumer by the requested dimension", () => {
    const shares = toFamilyShares([
      group({ family: "OneC", cpuPercent: 80, ramBytes: 100 }),
      group({ family: "Mssql", cpuPercent: 20, ramBytes: 900 }),
    ]);
    expect(dominantFamily(shares, "cpu")?.family).toBe("OneC");
    expect(dominantFamily(shares, "ram")?.family).toBe("Mssql");
  });

  it("returns null when there is no consumption", () => {
    const shares = toFamilyShares([group({ family: "OneC", cpuPercent: 0, ramBytes: 0 })]);
    expect(dominantFamily(shares, "cpu")).toBeNull();
  });
});

describe("computeVerdict", () => {
  it("is measuring on the first poll regardless of values", () => {
    const v = computeVerdict(
      snapshot({ measuring: true, cpu: { totalPercent: 99, queueLength: 9 } })
    );
    expect(v).toEqual({ level: "measuring", resource: null, culpritFamily: null });
  });

  it("is ok when all three resources are healthy", () => {
    expect(computeVerdict(snapshot({})).level).toBe("ok");
  });

  it("names the cpu bottleneck and its dominant family", () => {
    const v = computeVerdict(
      snapshot({
        cpu: { totalPercent: 95, queueLength: 6 },
        processGroups: [
          group({ family: "OneC", cpuPercent: 90, ramBytes: 100 }),
          group({ family: "Mssql", cpuPercent: 10, ramBytes: 100 }),
        ],
      })
    );
    expect(v.level).toBe("crit");
    expect(v.resource).toBe("cpu");
    expect(v.culpritFamily).toBe("OneC");
  });

  it("reports a disk bottleneck without a culprit family", () => {
    const v = computeVerdict(
      snapshot({ disk: { avgReadSecPerOp: 0.03, avgWriteSecPerOp: 0, queueLength: 0 } })
    );
    expect(v.resource).toBe("disk");
    expect(v.culpritFamily).toBeNull();
  });
});
