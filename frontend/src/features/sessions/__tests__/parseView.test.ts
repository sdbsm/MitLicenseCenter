import { describe, it, expect } from "vitest";
import { parseView } from "../useSessionsPage";

// MLC-196b: страница «Сеансы» — дом темы лицензий с тремя видами; активный вид в URL
// (?view=). Парсер различает три значения и сводит неизвестное к дефолту `byTenant`,
// чтобы битый/устаревший URL не ронял страницу.
describe("parseView", () => {
  it("defaults to byTenant when the view param is absent", () => {
    expect(parseView(new URLSearchParams())).toBe("byTenant");
  });

  it("reads `live`", () => {
    expect(parseView(new URLSearchParams("view=live"))).toBe("live");
  });

  it("reads `usage` (license-usage view, dissolved Reports)", () => {
    expect(parseView(new URLSearchParams("view=usage"))).toBe("usage");
  });

  it("treats an unknown view value as the default byTenant", () => {
    expect(parseView(new URLSearchParams("view=bogus"))).toBe("byTenant");
  });
});
