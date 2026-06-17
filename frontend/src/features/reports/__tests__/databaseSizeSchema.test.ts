import { describe, expect, it } from "vitest";
import {
  databaseSizeSeriesResponseSchema,
  databaseSizeTenantRowSchema,
  databaseSizeTenantSeriesResponseSchema,
} from "../types";

// MLC-185f / урок MLC-067/071 + ADR-32: backend опускает null-поля на wire. Строка
// «без клиента» приходит БЕЗ ключей tenantId/tenantName — Zod через omittable() должен
// принять отсутствие и нормализовать в null, а не упасть на боевом ответе.
describe("databaseSizeTenantRowSchema (omit-null)", () => {
  it("accepts a row with tenantId/tenantName present", () => {
    const row = databaseSizeTenantRowSchema.parse({
      tenantId: "t1",
      tenantName: "Acme",
      dataBytes: 100,
      logBytes: 20,
      totalBytes: 120,
      databaseCount: 2,
    });
    expect(row.tenantId).toBe("t1");
    expect(row.tenantName).toBe("Acme");
  });

  it("normalizes the omitted tenant keys («без клиента») to null", () => {
    // Ключи tenantId/tenantName ОТСУТСТВУЮТ — именно так приходит агрегат без клиента.
    const row = databaseSizeTenantRowSchema.parse({
      dataBytes: 50,
      logBytes: 10,
      totalBytes: 60,
      databaseCount: 1,
    });
    expect(row.tenantId).toBeNull();
    expect(row.tenantName).toBeNull();
  });

  it("also accepts an explicit null for the tenant keys", () => {
    const row = databaseSizeTenantRowSchema.parse({
      tenantId: null,
      tenantName: null,
      dataBytes: 50,
      logBytes: 10,
      totalBytes: 60,
      databaseCount: 1,
    });
    expect(row.tenantId).toBeNull();
    expect(row.tenantName).toBeNull();
  });
});

describe("databaseSizeSeriesResponseSchema", () => {
  it("parses a host summary with a points series and tenant breakdown", () => {
    const parsed = databaseSizeSeriesResponseSchema.parse({
      points: [{ atUtc: "2026-06-01T00:00:00Z", dataBytes: 100, logBytes: 20, totalBytes: 120 }],
      tenants: [{ dataBytes: 50, logBytes: 10, totalBytes: 60, databaseCount: 1 }],
      fromUtc: "2026-06-01T00:00:00Z",
      toUtc: "2026-06-07T23:59:59Z",
      clamped: false,
      maxSpanDays: 31,
    });
    expect(parsed.points).toHaveLength(1);
    expect(parsed.tenants[0]?.tenantId).toBeNull();
  });

  it("parses an empty period (no points, no tenants) as a valid 200", () => {
    const parsed = databaseSizeSeriesResponseSchema.parse({
      points: [],
      tenants: [],
      fromUtc: "2026-06-01T00:00:00Z",
      toUtc: "2026-06-07T23:59:59Z",
      clamped: false,
      maxSpanDays: 31,
    });
    expect(parsed.points).toEqual([]);
    expect(parsed.tenants).toEqual([]);
  });
});

describe("databaseSizeTenantSeriesResponseSchema", () => {
  it("parses a tenant drill-down with a databases table", () => {
    const parsed = databaseSizeTenantSeriesResponseSchema.parse({
      points: [{ atUtc: "2026-06-01T00:00:00Z", dataBytes: 100, logBytes: 20, totalBytes: 120 }],
      databases: [{ databaseName: "acme_db", dataBytes: 100, logBytes: 20, totalBytes: 120 }],
      fromUtc: "2026-06-01T00:00:00Z",
      toUtc: "2026-06-07T23:59:59Z",
      clamped: true,
      maxSpanDays: 31,
    });
    expect(parsed.databases[0]?.databaseName).toBe("acme_db");
    expect(parsed.clamped).toBe(true);
  });
});
