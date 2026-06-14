import { describe, expect, it } from "vitest";
import { filtersToUrl, parseFiltersFromUrl } from "../auditUrlState";
import type { AuditFilters } from "../types";

const base: AuditFilters = {
  actionType: null,
  tenantId: null,
  from: null,
  to: null,
  search: null,
  initiator: null,
  page: 1,
  pageSize: 50,
};

describe("auditUrlState — search / initiator (MLC-129)", () => {
  it("сериализует search и initiator при непустом значении", () => {
    const params = filtersToUrl({ ...base, search: "клиент", initiator: "System" });
    expect(params.get("search")).toBe("клиент");
    expect(params.get("initiator")).toBe("System");
  });

  it("опускает search и initiator при null", () => {
    const params = filtersToUrl(base);
    expect(params.has("search")).toBe(false);
    expect(params.has("initiator")).toBe(false);
  });

  it("разбирает search и initiator из URL", () => {
    const filters = parseFiltersFromUrl(new URLSearchParams("search=альфа&initiator=admin"));
    expect(filters.search).toBe("альфа");
    expect(filters.initiator).toBe("admin");
  });

  it("round-trip сохраняет все фильтры", () => {
    const original: AuditFilters = {
      ...base,
      actionType: "TenantCreated",
      tenantId: "11111111-1111-1111-1111-111111111111",
      from: "2026-01-01",
      to: "2026-02-01",
      search: "поиск",
      initiator: "operator",
      page: 3,
      pageSize: 25,
    };
    const restored = parseFiltersFromUrl(filtersToUrl(original));
    expect(restored).toEqual(original);
  });

  it("отсутствие search/initiator в URL → null", () => {
    const filters = parseFiltersFromUrl(new URLSearchParams(""));
    expect(filters.search).toBeNull();
    expect(filters.initiator).toBeNull();
  });
});
