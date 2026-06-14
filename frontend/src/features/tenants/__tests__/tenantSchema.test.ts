import { describe, expect, it } from "vitest";
import { tenantSchema } from "../types";

/**
 * MLC-136 (R12c). Граница `tenantSchema` несёт `rowVersion` — токен оптимистической
 * блокировки (SQL Server rowversion → base64-строка). API опускает null-поля
 * (`JsonIgnoreCondition.WhenWritingNull`, [[api-omits-null-fields]]): под InMemory-тестами
 * / до первой записи токен ОТСУТСТВУЕТ в ответе, а не приходит явным `null`. Схема обязана
 * принимать оба варианта (`omittable`) и нормализовать отсутствие в `null`.
 */
describe("tenantSchema.rowVersion", () => {
  const base = {
    id: "11111111-1111-1111-1111-111111111111",
    name: "Acme",
    maxConcurrentLicenses: 10,
    isActive: true,
    createdAt: "2026-06-14T12:00:00Z",
    infobaseCount: 0,
  };

  it("принимает ответ с rowVersion (base64-строкой)", () => {
    const parsed = tenantSchema.parse({ ...base, rowVersion: "AAAAAAAAB9E=" });
    expect(parsed.rowVersion).toBe("AAAAAAAAB9E=");
  });

  it("принимает ответ БЕЗ ключа rowVersion (omit-null) → rowVersion === null", () => {
    const parsed = tenantSchema.parse(base);
    expect(parsed.rowVersion).toBeNull();
  });

  it("принимает ответ с явным rowVersion: null → null", () => {
    const parsed = tenantSchema.parse({ ...base, rowVersion: null });
    expect(parsed.rowVersion).toBeNull();
  });
});
