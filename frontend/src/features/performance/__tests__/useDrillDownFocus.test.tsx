import { act, renderHook } from "@testing-library/react";
import { describe, expect, it } from "vitest";
import type { Verdict } from "../attribution";
import { autoFocusLayer, useDrillDownFocus } from "../useDrillDownFocus";

function verdict(culpritFamily: string | null, level: Verdict["level"] = "warn"): Verdict {
  return { level, resource: "cpu", culpritFamily };
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
