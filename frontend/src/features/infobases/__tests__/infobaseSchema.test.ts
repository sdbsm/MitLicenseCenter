import { describe, expect, it } from "vitest";
import { infobaseListItemSchema, infobaseSchema, publicationSchema } from "../types";

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

/**
 * MLC-185d. Элемент списка несёт текущий размер базы (currentDataBytes/currentLogBytes,
 * байты) из последнего снимка телеметрии. API опускает поля при null
 * ([[api-omits-null-fields]]): пока снимка нет (джоба не отработала / база недоступна) их
 * НЕ будет в ответе. Схема обязана принимать оба варианта (omittable) и нормализовать в null,
 * иначе FE упал бы на боевом ответе без полей.
 */
describe("infobaseListItemSchema.current*Bytes", () => {
  const base = {
    id: "11111111-1111-1111-1111-111111111111",
    tenantId: "22222222-2222-2222-2222-222222222222",
    tenantName: "Acme",
    name: "Acme BP",
    clusterInfobaseId: "33333333-3333-3333-3333-333333333333",
    databaseName: "acme_bp",
    status: "Active",
    createdAt: "2026-06-14T12:00:00Z",
    publication: {
      id: "44444444-4444-4444-4444-444444444444",
      infobaseId: "11111111-1111-1111-1111-111111111111",
      siteName: "Default Web Site",
      virtualPath: "/acme",
      platformVersion: "8.3.23.1865",
      source: "Webinst",
      createdAt: "2026-06-14T12:00:00Z",
      lastCheckStatus: "Unknown",
    },
  };

  it("принимает ответ с размерами (числами)", () => {
    const parsed = infobaseListItemSchema.parse({
      ...base,
      currentDataBytes: 1_073_741_824,
      currentLogBytes: 268_435_456,
    });
    expect(parsed.currentDataBytes).toBe(1_073_741_824);
    expect(parsed.currentLogBytes).toBe(268_435_456);
  });

  it("принимает ответ БЕЗ полей размера (omit-null) → undefined-источник нормализован в null", () => {
    const parsed = infobaseListItemSchema.parse(base);
    expect(parsed.currentDataBytes).toBeNull();
    expect(parsed.currentLogBytes).toBeNull();
  });

  it("принимает ответ с явными null → null", () => {
    const parsed = infobaseListItemSchema.parse({
      ...base,
      currentDataBytes: null,
      currentLogBytes: null,
    });
    expect(parsed.currentDataBytes).toBeNull();
    expect(parsed.currentLogBytes).toBeNull();
  });
});
