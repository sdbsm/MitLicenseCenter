import { describe, expect, it } from "vitest";
import { infobaseSchema, publicationSchema } from "../types";

/**
 * MLC-151. Границы `infobaseSchema` и `publicationSchema` несут `rowVersion` — токен
 * оптимистической блокировки (SQL Server rowversion → base64-строка). API опускает
 * null-поля (`JsonIgnoreCondition.WhenWritingNull`, [[api-omits-null-fields]]): под
 * InMemory-тестами / до первой записи токен ОТСУТСТВУЕТ в ответе, а не приходит явным
 * `null`. Схемы обязаны принимать оба варианта (`omittable`) и нормализовать в `null`.
 * Зеркаль tenantSchema.rowVersion (MLC-136).
 */
describe("infobaseSchema.rowVersion", () => {
  const base = {
    id: "11111111-1111-1111-1111-111111111111",
    tenantId: "22222222-2222-2222-2222-222222222222",
    name: "Acme BP",
    clusterInfobaseId: "33333333-3333-3333-3333-333333333333",
    databaseName: "acme_bp",
    status: "Active",
    createdAt: "2026-06-14T12:00:00Z",
  };

  it("принимает ответ с rowVersion (base64-строкой)", () => {
    const parsed = infobaseSchema.parse({ ...base, rowVersion: "AAAAAAAAB9E=" });
    expect(parsed.rowVersion).toBe("AAAAAAAAB9E=");
  });

  it("принимает ответ БЕЗ ключа rowVersion (omit-null) → rowVersion === null", () => {
    const parsed = infobaseSchema.parse(base);
    expect(parsed.rowVersion).toBeNull();
  });

  it("принимает ответ с явным rowVersion: null → null", () => {
    const parsed = infobaseSchema.parse({ ...base, rowVersion: null });
    expect(parsed.rowVersion).toBeNull();
  });
});

describe("publicationSchema.rowVersion", () => {
  const base = {
    id: "44444444-4444-4444-4444-444444444444",
    infobaseId: "11111111-1111-1111-1111-111111111111",
    siteName: "Default Web Site",
    virtualPath: "/acme",
    platformVersion: "8.3.23.1865",
    source: "Webinst",
    createdAt: "2026-06-14T12:00:00Z",
    lastCheckStatus: "Unknown",
  };

  it("принимает ответ с rowVersion (base64-строкой)", () => {
    const parsed = publicationSchema.parse({ ...base, rowVersion: "AAAAAAAAB9E=" });
    expect(parsed.rowVersion).toBe("AAAAAAAAB9E=");
  });

  it("принимает ответ БЕЗ ключа rowVersion (omit-null) → rowVersion === null", () => {
    const parsed = publicationSchema.parse(base);
    expect(parsed.rowVersion).toBeNull();
  });

  it("принимает ответ с явным rowVersion: null → null", () => {
    const parsed = publicationSchema.parse({ ...base, rowVersion: null });
    expect(parsed.rowVersion).toBeNull();
  });
});
