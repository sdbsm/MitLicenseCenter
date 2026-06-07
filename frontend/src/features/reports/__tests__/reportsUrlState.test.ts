import { describe, it, expect } from "vitest";
import {
  buildBackendRange,
  filtersToUrl,
  monthToRange,
  parseFiltersFromUrl,
  shiftMonth,
} from "../reportsUrlState";
import type { ReportsFilters } from "../types";

describe("parseFiltersFromUrl", () => {
  it("reads from/to/tenant from the query string", () => {
    const params = new URLSearchParams("from=2026-06-01&to=2026-06-07&tenant=abc");
    expect(parseFiltersFromUrl(params)).toEqual({
      from: "2026-06-01",
      to: "2026-06-07",
      tenantId: "abc",
    });
  });

  it("defaults absent params to null", () => {
    expect(parseFiltersFromUrl(new URLSearchParams())).toEqual({
      from: null,
      to: null,
      tenantId: null,
    });
  });
});

describe("filtersToUrl", () => {
  it("omits empty values and maps tenantId to the `tenant` key", () => {
    const filters: ReportsFilters = { from: "2026-06-01", to: null, tenantId: "abc" };
    expect(filtersToUrl(filters).toString()).toBe("from=2026-06-01&tenant=abc");
  });

  it("produces an empty query for cleared filters", () => {
    const filters: ReportsFilters = { from: null, to: null, tenantId: null };
    expect(filtersToUrl(filters).toString()).toBe("");
  });
});

describe("buildBackendRange", () => {
  it("expands date-only bounds to ISO start/end of day", () => {
    const range = buildBackendRange({ from: "2026-06-01", to: "2026-06-07", tenantId: null });
    expect(range).toEqual({
      from: "2026-06-01T00:00:00Z",
      to: "2026-06-07T23:59:59Z",
    });
  });

  it("keeps absent bounds null so the server applies the default window", () => {
    expect(buildBackendRange({ from: null, to: null, tenantId: "x" })).toEqual({
      from: null,
      to: null,
    });
  });
});

describe("monthToRange", () => {
  it("spans the full 31-day month", () => {
    expect(monthToRange("2026-01")).toEqual({ from: "2026-01-01", to: "2026-01-31" });
  });

  it("spans the full 30-day month", () => {
    expect(monthToRange("2026-04")).toEqual({ from: "2026-04-01", to: "2026-04-30" });
  });

  it("ends on the 28th in a common-year February", () => {
    expect(monthToRange("2026-02")).toEqual({ from: "2026-02-01", to: "2026-02-28" });
  });

  it("ends on the 29th in a leap-year February", () => {
    expect(monthToRange("2024-02")).toEqual({ from: "2024-02-01", to: "2024-02-29" });
  });
});

describe("shiftMonth", () => {
  it("steps back across the year boundary", () => {
    expect(shiftMonth("2026-01", -1)).toBe("2025-12");
  });

  it("steps forward across the year boundary", () => {
    expect(shiftMonth("2026-12", 1)).toBe("2027-01");
  });

  it("steps within a year", () => {
    expect(shiftMonth("2026-06", 1)).toBe("2026-07");
    expect(shiftMonth("2026-06", -1)).toBe("2026-05");
  });
});
