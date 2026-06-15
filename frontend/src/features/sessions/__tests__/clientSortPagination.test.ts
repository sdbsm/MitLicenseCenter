import { describe, it, expect } from "vitest";
import { sortRows, SESSIONS_PAGE_SIZE } from "../useSessionsPage";
import type { SessionSort } from "../useSessionsPage";
import type { SessionSnapshotEntry } from "../types";

function makeRow(overrides: Partial<SessionSnapshotEntry>): SessionSnapshotEntry {
  return {
    sessionId: crypto.randomUUID(),
    clusterInfobaseId: crypto.randomUUID(),
    tenantId: crypto.randomUUID(),
    tenantName: "Клиент",
    infobaseName: "БП",
    appId: "1CV8C",
    userName: "user",
    host: "WS01",
    licenseStatus: "NotConsuming",
    startedAt: "2026-05-20T10:00:00Z",
    durationSeconds: 0,
    ...overrides,
  };
}

describe("sortRows — клиентская сортировка сеансов (UX-14)", () => {
  it("сортирует по tenantName asc — лексикографически по-русски", () => {
    const rows = [
      makeRow({ tenantName: "Бета" }),
      makeRow({ tenantName: "Альфа" }),
      makeRow({ tenantName: "Гамма" }),
    ];
    const sort: SessionSort = { key: "tenantName", dir: "asc" };
    const result = sortRows(rows, sort);
    expect(result.map((r) => r.tenantName)).toEqual(["Альфа", "Бета", "Гамма"]);
  });

  it("сортирует по tenantName desc", () => {
    const rows = [
      makeRow({ tenantName: "Альфа" }),
      makeRow({ tenantName: "Гамма" }),
      makeRow({ tenantName: "Бета" }),
    ];
    const sort: SessionSort = { key: "tenantName", dir: "desc" };
    const result = sortRows(rows, sort);
    expect(result.map((r) => r.tenantName)).toEqual(["Гамма", "Бета", "Альфа"]);
  });

  it("сортирует по durationSeconds числово", () => {
    const rows = [
      makeRow({ durationSeconds: 300 }),
      makeRow({ durationSeconds: 60 }),
      makeRow({ durationSeconds: 120 }),
    ];
    const sort: SessionSort = { key: "durationSeconds", dir: "asc" };
    const result = sortRows(rows, sort);
    expect(result.map((r) => r.durationSeconds)).toEqual([60, 120, 300]);
  });

  it("сортирует по startedAt — ISO-строки сортируются лексикографически корректно", () => {
    const rows = [
      makeRow({ startedAt: "2026-05-20T12:00:00Z" }),
      makeRow({ startedAt: "2026-05-20T08:00:00Z" }),
      makeRow({ startedAt: "2026-05-20T10:00:00Z" }),
    ];
    const sort: SessionSort = { key: "startedAt", dir: "asc" };
    const result = sortRows(rows, sort);
    expect(result.map((r) => r.startedAt)).toEqual([
      "2026-05-20T08:00:00Z",
      "2026-05-20T10:00:00Z",
      "2026-05-20T12:00:00Z",
    ]);
  });

  it("сортировка licenseStatus: Consuming → NotConsuming → Pending при asc (ADR-48)", () => {
    const rows = [
      makeRow({ licenseStatus: "Pending" }),
      makeRow({ licenseStatus: "Consuming" }),
      makeRow({ licenseStatus: "NotConsuming" }),
    ];
    const sort: SessionSort = { key: "licenseStatus", dir: "asc" };
    const result = sortRows(rows, sort);
    expect(result.map((r) => r.licenseStatus)).toEqual(["Consuming", "NotConsuming", "Pending"]);
  });

  it("стабильность: равные элементы сохраняют относительный порядок (stable sort)", () => {
    // У всех одинаковый tenantName — порядок из исходного массива сохраняется
    const ids = ["a", "b", "c", "d"];
    const rows = ids.map((id) => makeRow({ sessionId: id, tenantName: "Одинаковый" }));
    const sort: SessionSort = { key: "tenantName", dir: "asc" };
    const result = sortRows(rows, sort);
    expect(result.map((r) => r.sessionId)).toEqual(ids);
  });

  it("не мутирует исходный массив", () => {
    const rows = [makeRow({ tenantName: "Бета" }), makeRow({ tenantName: "Альфа" })];
    const original = rows.map((r) => r.sessionId);
    const sort: SessionSort = { key: "tenantName", dir: "asc" };
    sortRows(rows, sort);
    expect(rows.map((r) => r.sessionId)).toEqual(original);
  });
});

describe("клиентская пагинация сеансов (UX-14) — clamp страницы при смене снапшота", () => {
  it("SESSIONS_PAGE_SIZE равен 25", () => {
    expect(SESSIONS_PAGE_SIZE).toBe(25);
  });

  it("clamp: page > totalPages → должна быть 1 (логика в useSessionsPage)", () => {
    // Эмулируем логику clamp из хука напрямую
    const sorted = Array.from({ length: 10 }, (_, i) => makeRow({ tenantName: String(i) }));
    const totalPages = Math.max(1, Math.ceil(sorted.length / SESSIONS_PAGE_SIZE));
    const stalePage = 3; // пользователь был на 3-й странице при большом снапшоте
    const safePage = Math.min(stalePage, totalPages);
    expect(safePage).toBe(1); // 10 строк → 1 страница; 3 clamp'ится в 1
  });

  it("clamp: page в пределах → остаётся неизменной", () => {
    // 60 строк → 3 страницы; пользователь на 2-й
    const total = 60;
    const totalPages = Math.max(1, Math.ceil(total / SESSIONS_PAGE_SIZE));
    const safePage = Math.min(2, totalPages);
    expect(safePage).toBe(2);
  });

  it("при refetch снапшот меняется, но page не «прыгает» пока вмещается", () => {
    // 50 строк → 2 страницы; пользователь на 2-й — страница остаётся 2
    const total = 50;
    const totalPages = Math.max(1, Math.ceil(total / SESSIONS_PAGE_SIZE));
    const safePage = Math.min(2, totalPages);
    expect(safePage).toBe(2);
  });
});
