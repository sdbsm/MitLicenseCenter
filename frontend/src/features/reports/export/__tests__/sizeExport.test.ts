import { describe, expect, it } from "vitest";
import { sizeExportFilename, sizeScopeLabel, type SizeExportData } from "../sizeExport";

const base: SizeExportData = {
  scope: "all",
  points: [],
  // Полдень UTC — date-only не плывёт ни в одной реальной TZ.
  fromUtc: "2026-06-01T12:00:00Z",
  toUtc: "2026-06-07T12:00:00Z",
  tenants: [],
  databases: [],
};

describe("sizeExportFilename", () => {
  it("uses «all» scope and a date-only range for the summary", () => {
    expect(sizeExportFilename(base, "csv")).toBe("database-size_all_2026-06-01_2026-06-07.csv");
  });

  it("slugifies the client name for the drill-down scope", () => {
    expect(sizeExportFilename({ ...base, scope: { tenantName: "ООО Ромашка" } }, "xlsx")).toBe(
      "database-size_ооо-ромашка_2026-06-01_2026-06-07.xlsx"
    );
  });

  it("strips filesystem-unsafe characters from the client name", () => {
    expect(sizeExportFilename({ ...base, scope: { tenantName: 'a/b:c*?"<>|d' } }, "csv")).toBe(
      "database-size_abcd_2026-06-01_2026-06-07.csv"
    );
  });

  it("falls back to «client» for an empty or null name", () => {
    expect(sizeExportFilename({ ...base, scope: { tenantName: null } }, "csv")).toBe(
      "database-size_client_2026-06-01_2026-06-07.csv"
    );
  });
});

describe("sizeScopeLabel", () => {
  it("labels the summary scope", () => {
    expect(sizeScopeLabel("all")).toBe("Все клиенты");
  });

  it("uses the tenant name for the drill-down scope", () => {
    expect(sizeScopeLabel({ tenantName: "ООО Ромашка" })).toBe("ООО Ромашка");
  });

  it("falls back for a null tenant name", () => {
    expect(sizeScopeLabel({ tenantName: null })).toBe("Клиент");
  });
});
