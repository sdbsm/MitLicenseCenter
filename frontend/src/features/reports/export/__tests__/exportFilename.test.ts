import { describe, expect, it } from "vitest";
import type { LicenseUsageSeriesResponse } from "../../types";
import { exportFilename } from "../exportFilename";

const data: LicenseUsageSeriesResponse = {
  buckets: [],
  // Полдень UTC — дата date-only не плывёт ни в одной реальной TZ (формат локальный).
  fromUtc: "2026-06-01T12:00:00Z",
  toUtc: "2026-06-07T12:00:00Z",
  peakConsumed: 0,
  peakLimit: 0,
  peakAtUtc: null,
  averageConsumed: 0,
};

describe("exportFilename", () => {
  it("uses «all» scope for the summary and a date-only range", () => {
    expect(exportFilename("all", data, "csv")).toBe("license-usage_all_2026-06-01_2026-06-07.csv");
  });

  it("slugifies the client name for the drill-down scope", () => {
    expect(exportFilename({ tenantName: "ООО Ромашка" }, data, "xlsx")).toBe(
      "license-usage_ооо-ромашка_2026-06-01_2026-06-07.xlsx"
    );
  });

  it("strips filesystem-unsafe characters from the client name", () => {
    expect(exportFilename({ tenantName: 'a/b:c*?"<>|d' }, data, "csv")).toBe(
      "license-usage_abcd_2026-06-01_2026-06-07.csv"
    );
  });

  it("falls back to «client» for an empty or null name", () => {
    expect(exportFilename({ tenantName: null }, data, "csv")).toBe(
      "license-usage_client_2026-06-01_2026-06-07.csv"
    );
    expect(exportFilename({ tenantName: "   " }, data, "csv")).toBe(
      "license-usage_client_2026-06-01_2026-06-07.csv"
    );
  });
});
