import { describe, it, expect } from "vitest";
import { buildBackendRange, filtersToUrl, parseFiltersFromUrl } from "../reportsUrlState";
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
