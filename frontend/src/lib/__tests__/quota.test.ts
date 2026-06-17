import { describe, expect, it } from "vitest";
import {
  QUOTA_DANGER_THRESHOLD,
  QUOTA_WARNING_THRESHOLD,
  quotaDisplay,
  quotaLabel,
  quotaPercent,
  quotaSeverity,
  severityToProgressClass,
  severityToStatusBadgeVariant,
} from "../quota";

describe("quotaPercent", () => {
  it("returns 0 when limit is 0 (безлимит)", () => {
    expect(quotaPercent(10, 0)).toBe(0);
  });
  it("returns 0 when limit is negative (безлимит)", () => {
    expect(quotaPercent(5, -1)).toBe(0);
  });
  it("returns 0 when consumed is 0", () => {
    expect(quotaPercent(0, 10)).toBe(0);
  });
  it("rounds to nearest integer", () => {
    // 7 / 10 = 70
    expect(quotaPercent(7, 10)).toBe(70);
  });
  it("returns 100 when consumed equals limit", () => {
    expect(quotaPercent(10, 10)).toBe(100);
  });
  it("returns > 100 when over limit", () => {
    expect(quotaPercent(11, 10)).toBe(110);
  });
  it("rounds correctly at 74.5% → 75", () => {
    // consumed=149, limit=200 → 74.5 → 75 (rounded)
    expect(quotaPercent(149, 200)).toBe(75);
  });
  it("returns 74 at below-warning boundary", () => {
    // consumed=148, limit=200 → 74 (just below warning)
    expect(quotaPercent(148, 200)).toBe(74);
  });
});

describe("quotaSeverity", () => {
  it("is 'ok' when limit is 0 (безлимит)", () => {
    expect(quotaSeverity(999, 0)).toBe("ok");
  });
  it("is 'ok' when consumed is 0", () => {
    expect(quotaSeverity(0, 100)).toBe("ok");
  });
  it("is 'ok' below warning threshold (74%)", () => {
    expect(quotaSeverity(74, 100)).toBe("ok");
  });
  it("is 'warning' at exact warning threshold (75%)", () => {
    expect(quotaSeverity(QUOTA_WARNING_THRESHOLD, 100)).toBe("warning");
  });
  it("is 'warning' between thresholds (89%)", () => {
    expect(quotaSeverity(89, 100)).toBe("warning");
  });
  it("is 'danger' at exact danger threshold (90%)", () => {
    expect(quotaSeverity(QUOTA_DANGER_THRESHOLD, 100)).toBe("danger");
  });
  it("is 'danger' above danger threshold (100%)", () => {
    expect(quotaSeverity(100, 100)).toBe("danger");
  });
  it("is 'danger' over limit (>100%)", () => {
    expect(quotaSeverity(150, 100)).toBe("danger");
  });
});

describe("severityToStatusBadgeVariant", () => {
  it("maps 'danger' → 'danger'", () => {
    expect(severityToStatusBadgeVariant("danger")).toBe("danger");
  });
  it("maps 'warning' → 'warning'", () => {
    expect(severityToStatusBadgeVariant("warning")).toBe("warning");
  });
  it("maps 'ok' → 'neutral'", () => {
    expect(severityToStatusBadgeVariant("ok")).toBe("neutral");
  });
});

describe("severityToProgressClass", () => {
  it("returns rose class for 'danger'", () => {
    expect(severityToProgressClass("danger")).toContain("rose-500");
  });
  it("returns amber class for 'warning'", () => {
    expect(severityToProgressClass("warning")).toContain("amber-500");
  });
  it("returns empty string for 'ok'", () => {
    expect(severityToProgressClass("ok")).toBe("");
  });
});

describe("quotaLabel — ярлык по факту consumed vs limit (MLC-188)", () => {
  it("null при безлимите (limit<=0)", () => {
    expect(quotaLabel(999, 0)).toBeNull();
  });
  it("null ниже warning-порога (74%)", () => {
    expect(quotaLabel(74, 100)).toBeNull();
  });
  it("'nearLimit' на warning (75%, ниже лимита)", () => {
    expect(quotaLabel(75, 100)).toBe("nearLimit");
  });
  it("'nearLimit' на danger, но ниже лимита (9 из 10 = 90%)", () => {
    expect(quotaLabel(9, 10)).toBe("nearLimit");
  });
  it("'atLimit' ровно по лимиту (10 из 10) — НЕ превышение", () => {
    expect(quotaLabel(10, 10)).toBe("atLimit");
  });
  it("'exceeded' только при consumed > limit (11 из 10)", () => {
    expect(quotaLabel(11, 10)).toBe("exceeded");
  });
});

describe("quotaDisplay — boundary table", () => {
  const cases = [
    { consumed: 0, limit: 10, expectedSeverity: "ok", expectedPercent: 0, expectedLabel: null },
    { consumed: 74, limit: 100, expectedSeverity: "ok", expectedPercent: 74, expectedLabel: null },
    {
      consumed: 75,
      limit: 100,
      expectedSeverity: "warning",
      expectedPercent: 75,
      expectedLabel: "nearLimit",
    },
    {
      consumed: 89,
      limit: 100,
      expectedSeverity: "warning",
      expectedPercent: 89,
      expectedLabel: "nearLimit",
    },
    {
      consumed: 90,
      limit: 100,
      expectedSeverity: "danger",
      expectedPercent: 90,
      expectedLabel: "nearLimit",
    },
    {
      consumed: 100,
      limit: 100,
      expectedSeverity: "danger",
      expectedPercent: 100,
      expectedLabel: "atLimit",
    },
    {
      consumed: 150,
      limit: 100,
      expectedSeverity: "danger",
      expectedPercent: 150,
      expectedLabel: "exceeded",
    },
    { consumed: 10, limit: 0, expectedSeverity: "ok", expectedPercent: 0, expectedLabel: null },
  ] as const;

  for (const { consumed, limit, expectedSeverity, expectedPercent, expectedLabel } of cases) {
    it(`consumed=${consumed}, limit=${limit} → severity=${expectedSeverity}, percent=${expectedPercent}, label=${expectedLabel}`, () => {
      const result = quotaDisplay(consumed, limit);
      expect(result.severity).toBe(expectedSeverity);
      expect(result.percent).toBe(expectedPercent);
      expect(result.label).toBe(expectedLabel);
    });
  }
});
