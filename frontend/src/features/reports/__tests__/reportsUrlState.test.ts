import { describe, it, expect } from "vitest";
import {
  buildBackendRange,
  filtersToUrl,
  formatBucketAxisLabel,
  monthToRange,
  parseFiltersFromUrl,
  shiftMonth,
} from "../reportsUrlState";
import type { ReportsFilters } from "../types";

// MLC-177: границы периода считаются в ЛОКАЛЬНОМ поясе браузера. Прогон прибит к
// Europe/Moscow (UTC+3, без DST) через vitest.config.ts → env.TZ; без фиксации пояса
// эти ожидания зелёные только на UTC-раннере. Проверяем, что пояс действительно UTC+3
// (страховка от случайной правки конфига), затем — что локальная полночь/конец суток
// уходят на сервер со сдвигом −3 ч.
const TZ_OFFSET_MINUTES = new Date("2026-06-01T00:00:00").getTimezoneOffset();

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
  it("runs under a fixed UTC+3 timezone (guards the env pin)", () => {
    // getTimezoneOffset = минуты, которые надо ПРИБАВИТЬ к локальному, чтобы получить
    // UTC; для UTC+3 это −180. Если конфиг TZ съедет — этот тест упадёт первым.
    expect(TZ_OFFSET_MINUTES).toBe(-180);
  });

  it("expands date-only bounds to LOCAL start/end of day, serialized as UTC", () => {
    const range = buildBackendRange({ from: "2026-06-01", to: "2026-06-07", tenantId: null });
    // Локальная полночь 2026-06-01 00:00 (UTC+3) → 2026-05-31T21:00:00Z.
    // Локальный конец суток 2026-06-07 23:59:59.999 (UTC+3) → 2026-06-07T20:59:59.999Z.
    expect(range).toEqual({
      from: "2026-05-31T21:00:00.000Z",
      to: "2026-06-07T20:59:59.999Z",
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

  it("month bounds expand to LOCAL month edges via buildBackendRange (no hour drift)", () => {
    // MLC-177: месяц/сутки идут через одни локальные хелперы — границы месяца не
    // «съезжают» на часы пояса. Январь 2026 (UTC+3): 01 00:00 → 2025-12-31T21:00Z,
    // 31 23:59:59.999 → 2026-01-31T20:59:59.999Z.
    const { from, to } = monthToRange("2026-01");
    expect(buildBackendRange({ from, to, tenantId: null })).toEqual({
      from: "2025-12-31T21:00:00.000Z",
      to: "2026-01-31T20:59:59.999Z",
    });
  });
});

describe("formatBucketAxisLabel", () => {
  it("renders the bucket UTC instant in the LOCAL timezone (UTC+3)", () => {
    // 2026-06-01T00:00:00Z → локально (UTC+3) 03:00 1 июня.
    expect(formatBucketAxisLabel("2026-06-01T00:00:00Z")).toBe("01.06 03:00");
  });

  it("rolls the local day forward when UTC instant is late-day", () => {
    // 2026-06-01T22:30:00Z → локально (UTC+3) 01:30 2 июня.
    expect(formatBucketAxisLabel("2026-06-01T22:30:00Z")).toBe("02.06 01:30");
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
