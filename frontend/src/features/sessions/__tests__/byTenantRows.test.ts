import { describe, it, expect } from "vitest";
import { buildByTenantRows, sortByTenantRows, type ByTenantRow } from "../byTenantRows";
import type { Tenant } from "@/features/tenants/types";

function makeTenant(overrides: Partial<Tenant>): Tenant {
  return {
    id: crypto.randomUUID(),
    name: "Клиент",
    maxConcurrentLicenses: 10,
    isActive: true,
    createdAt: "2026-01-01T00:00:00Z",
    updatedAt: null,
    infobaseCount: 0,
    rowVersion: null,
    ...overrides,
  };
}

describe("buildByTenantRows — склейка проекции «По клиентам» (MLC-196a)", () => {
  it("склеивает клиентов с потреблением из map; имя и лимит — из клиента", () => {
    const a = makeTenant({ id: "a", name: "Альфа", maxConcurrentLicenses: 10 });
    const b = makeTenant({ id: "b", name: "Бета", maxConcurrentLicenses: 5 });
    const consumed = new Map([
      ["a", 3],
      ["b", 4],
    ]);
    const rows = buildByTenantRows([a, b], consumed);
    const byId = new Map(rows.map((r) => [r.tenantId, r]));
    expect(byId.get("a")).toMatchObject({ tenantName: "Альфа", consumed: 3, limit: 10 });
    expect(byId.get("b")).toMatchObject({ tenantName: "Бета", consumed: 4, limit: 5 });
  });

  it("клиент без сеансов присутствует с consumed = 0", () => {
    const a = makeTenant({ id: "a", name: "Альфа" });
    const rows = buildByTenantRows([a], new Map());
    expect(rows).toHaveLength(1);
    expect(rows[0]).toMatchObject({ tenantId: "a", consumed: 0 });
  });

  it("пустой список клиентов → пустой результат", () => {
    expect(buildByTenantRows([], new Map([["a", 5]]))).toEqual([]);
  });

  it("консумпция по неизвестному клиенту игнорируется (нет такой строки)", () => {
    const a = makeTenant({ id: "a" });
    const rows = buildByTenantRows([a], new Map([["zzz", 99]]));
    expect(rows.map((r) => r.tenantId)).toEqual(["a"]);
    expect(rows[0].consumed).toBe(0);
  });
});

describe("sortByTenantRows — превышения сверху, затем consumed ↓ (MLC-196a)", () => {
  function row(p: Partial<ByTenantRow>): ByTenantRow {
    return { tenantId: crypto.randomUUID(), tenantName: "Х", consumed: 0, limit: 10, ...p };
  }

  it("превышения (consumed > limit) идут первыми, даже при меньшем consumed", () => {
    const over = row({ tenantName: "Превышен", consumed: 6, limit: 5 }); // превышение
    const big = row({ tenantName: "Большой", consumed: 50, limit: 100 }); // не превышение
    const result = sortByTenantRows([big, over]);
    expect(result[0].tenantName).toBe("Превышен");
    expect(result[1].tenantName).toBe("Большой");
  });

  it("внутри группы — по consumed убыванию", () => {
    const r1 = row({ tenantName: "A", consumed: 2, limit: 10 });
    const r2 = row({ tenantName: "B", consumed: 8, limit: 10 });
    const r3 = row({ tenantName: "C", consumed: 5, limit: 10 });
    const result = sortByTenantRows([r1, r2, r3]);
    expect(result.map((r) => r.consumed)).toEqual([8, 5, 2]);
  });

  it("при равном consumed — по имени клиента (по-русски)", () => {
    const r1 = row({ tenantName: "Гамма", consumed: 3, limit: 10 });
    const r2 = row({ tenantName: "Альфа", consumed: 3, limit: 10 });
    const r3 = row({ tenantName: "Бета", consumed: 3, limit: 10 });
    const result = sortByTenantRows([r1, r2, r3]);
    expect(result.map((r) => r.tenantName)).toEqual(["Альфа", "Бета", "Гамма"]);
  });

  it("равенство consumed === limit — НЕ превышение (atLimit, не сверху)", () => {
    const atLimit = row({ tenantName: "Ровно", consumed: 10, limit: 10 }); // достигнут, не превышен
    const over = row({ tenantName: "Сверх", consumed: 11, limit: 10 }); // превышение
    const result = sortByTenantRows([atLimit, over]);
    expect(result[0].tenantName).toBe("Сверх");
  });

  it("безлимит (limit <= 0) превышением быть не может, идёт по consumed", () => {
    const unlimited = row({ tenantName: "Безлимит", consumed: 1000, limit: 0 });
    const over = row({ tenantName: "Превышен", consumed: 6, limit: 5 });
    const result = sortByTenantRows([unlimited, over]);
    expect(result[0].tenantName).toBe("Превышен"); // превышение сверху, несмотря на меньший consumed
  });

  it("несколько превышений сортируются по consumed убыв. внутри своей группы", () => {
    const over1 = row({ tenantName: "O1", consumed: 12, limit: 10 });
    const over2 = row({ tenantName: "O2", consumed: 20, limit: 10 });
    const ok = row({ tenantName: "OK", consumed: 9, limit: 10 });
    const result = sortByTenantRows([over1, ok, over2]);
    expect(result.map((r) => r.tenantName)).toEqual(["O2", "O1", "OK"]);
  });

  it("не мутирует исходный массив", () => {
    const rows = [row({ tenantName: "B", consumed: 1 }), row({ tenantName: "A", consumed: 9 })];
    const snapshot = rows.map((r) => r.tenantName);
    sortByTenantRows(rows);
    expect(rows.map((r) => r.tenantName)).toEqual(snapshot);
  });
});
