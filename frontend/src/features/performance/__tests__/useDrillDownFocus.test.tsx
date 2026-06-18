import { act, renderHook } from "@testing-library/react";
import { describe, expect, it } from "vitest";
import type { FamilyShare, Verdict } from "../attribution";
import { autoFocusLayer, layerForResource, useDrillDownFocus } from "../useDrillDownFocus";

function verdict(culpritFamily: string | null, level: Verdict["level"] = "warn"): Verdict {
  return { level, resource: "cpu", culpritFamily };
}

// Доминирующей считается семья с максимальной долей (cpuShare/ramShare). Фикстуры
// задают долю напрямую — `dominantFamily` смотрит именно на неё.
function share(family: string, cpuShare: number, ramShare: number): FamilyShare {
  return { family, cpuPercent: 0, ramBytes: 0, processCount: 1, cpuShare, ramShare };
}

describe("autoFocusLayer", () => {
  it("focuses 1С layer when the culprit is OneC", () => {
    expect(autoFocusLayer(verdict("OneC"))).toBe("onec");
  });

  it("focuses SQL layer when the culprit is Mssql", () => {
    expect(autoFocusLayer(verdict("Mssql"))).toBe("sql");
  });

  it.each(["OsUpdate", "Antivirus", "Other"])(
    "falls back to host layer for non-1С/SQL culprit %s",
    (family) => {
      expect(autoFocusLayer(verdict(family))).toBe("host");
    }
  );

  it("falls back to host when there is no culprit (null)", () => {
    expect(autoFocusLayer(verdict(null))).toBe("host");
  });

  it("falls back to host for ok / measuring verdicts", () => {
    expect(autoFocusLayer({ level: "ok", resource: null, culpritFamily: null })).toBe("host");
    expect(autoFocusLayer({ level: "measuring", resource: null, culpritFamily: null })).toBe(
      "host"
    );
  });

  it("falls back to host for null/undefined verdict", () => {
    expect(autoFocusLayer(null)).toBe("host");
    expect(autoFocusLayer(undefined)).toBe("host");
  });

  it("falls back to host when the bottleneck resource is disk (culprit null)", () => {
    expect(autoFocusLayer({ level: "crit", resource: "disk", culpritFamily: null })).toBe("host");
  });
});

describe("layerForResource", () => {
  it("maps cpu to the layer of the dominant cpu family", () => {
    expect(layerForResource("cpu", [share("OneC", 0.7, 0.1), share("Other", 0.3, 0.9)])).toBe(
      "onec"
    );
    expect(layerForResource("cpu", [share("Mssql", 0.8, 0.1), share("OneC", 0.2, 0.9)])).toBe(
      "sql"
    );
    expect(layerForResource("cpu", [share("Other", 0.9, 0.1), share("OneC", 0.1, 0.9)])).toBe(
      "host"
    );
  });

  it("maps ram to the layer of the dominant ram family", () => {
    expect(layerForResource("ram", [share("OneC", 0.1, 0.7), share("Other", 0.9, 0.3)])).toBe(
      "onec"
    );
    expect(layerForResource("ram", [share("Mssql", 0.1, 0.8), share("OneC", 0.9, 0.2)])).toBe(
      "sql"
    );
    expect(layerForResource("ram", [share("Other", 0.1, 0.9), share("OneC", 0.9, 0.1)])).toBe(
      "host"
    );
  });

  it("always maps disk to the SQL layer (IO-stall lives there, no family attribution)", () => {
    expect(layerForResource("disk", [share("OneC", 0.9, 0.9)])).toBe("sql");
    expect(layerForResource("disk", [])).toBe("sql");
  });

  it("falls back to host for cpu/ram when there are no families", () => {
    expect(layerForResource("cpu", [])).toBe("host");
    expect(layerForResource("ram", [])).toBe("host");
  });
});

describe("useDrillDownFocus", () => {
  it("starts on the suggested layer and auto-follows verdict changes", () => {
    const { result, rerender } = renderHook(({ v }) => useDrillDownFocus(v), {
      initialProps: { v: verdict("OneC") as Verdict | null },
    });

    expect(result.current.layer).toBe("onec");

    rerender({ v: verdict("Mssql") });
    expect(result.current.layer).toBe("sql");
  });

  it("stops auto-following after the user pins a layer manually", () => {
    const { result, rerender } = renderHook(({ v }) => useDrillDownFocus(v), {
      initialProps: { v: verdict("OneC") as Verdict | null },
    });

    expect(result.current.layer).toBe("onec");

    act(() => result.current.setLayer("sql"));
    expect(result.current.layer).toBe("sql");

    // Verdict now points to host, but the manual pin must win.
    rerender({ v: verdict(null) });
    expect(result.current.layer).toBe("sql");
  });
});
