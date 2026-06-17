import { describe, expect, it } from "vitest";
import { filtersToUrl, parseFiltersFromUrl } from "../auditUrlState";
import type { AuditFilters } from "../types";

const base: AuditFilters = {
  actionType: null,
  tenantId: null,
  from: null,
  to: null,
  search: null,
  page: 1,
  pageSize: 50,
};

describe("auditUrlState — search (MLC-129, MLC-192)", () => {
  it("сериализует search при непустом значении", () => {
    const params = filtersToUrl({ ...base, search: "клиент" });
    expect(params.get("search")).toBe("клиент");
  });

  it("опускает search при null", () => {
    const params = filtersToUrl(base);
    expect(params.has("search")).toBe(false);
  });

  it("разбирает search из URL", () => {
    const filters = parseFiltersFromUrl(new URLSearchParams("search=альфа"));
    expect(filters.search).toBe("альфа");
  });

  it("round-trip сохраняет все фильтры", () => {
    const original: AuditFilters = {
      ...base,
      actionType: "TenantCreated",
      tenantId: "11111111-1111-1111-1111-111111111111",
      from: "2026-01-01",
      to: "2026-02-01",
      search: "поиск",
      page: 3,
      pageSize: 25,
    };
    const restored = parseFiltersFromUrl(filtersToUrl(original));
    expect(restored).toEqual(original);
  });

  it("отсутствие search в URL → null", () => {
    const filters = parseFiltersFromUrl(new URLSearchParams(""));
    expect(filters.search).toBeNull();
  });
});
