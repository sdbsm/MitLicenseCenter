import { describe, it, expect } from "vitest";
import {
  PLATFORM_VERSION_PATTERN,
  NAME_MAX_LENGTH,
  DATABASE_SERVER_MAX_LENGTH,
  DATABASE_NAME_MAX_LENGTH,
  SITE_NAME_MAX_LENGTH,
  VIRTUAL_PATH_MAX_LENGTH,
  PLATFORM_VERSION_MAX_LENGTH,
  PHYSICAL_PATH_MAX_LENGTH,
  buildInfobaseFormSchema,
} from "../validation";

// MLC-022 — parity-тест единого источника правил валидации. Golden-таблица версии
// платформы идентична backend'у (InfobasesValidationTests.cs); пины констант закреплены
// к литералам прозы-спеки docs/03_DOMAIN_MODEL.md (§3). Кто меняет regex/лимит на FE —
// ломает этот тест, пока спека не изменена осознанно. Codegen не вводится (MLC-025).

describe("validation — platformVersion (golden-таблица, парная backend'у)", () => {
  it.each([
    ["8.3.23.1865", true],
    ["8.3.24.1654", true],
    ["8.5.1.1302", true], // 1С 8.5 ранние сборки — build одноцифровой
    ["8.3.1.1865", true], // build одноцифровой — допустимо
    ["8.3.23.18", true], // короткая revision — допустимо
    ["10.0.10.0001", true],
    ["8.3", false],
    ["8.3.23", false],
    ["8.3.23.", false],
    ["", false],
    ["a.b.c.d", false],
    ["8.3.23.1865.0", false],
  ])("%s → %s", (value, expected) => {
    expect(PLATFORM_VERSION_PATTERN.test(value as string)).toBe(expected);
  });
});

describe("validation — пины констант к спеке 03_DOMAIN_MODEL.md", () => {
  it("regex версии платформы == документированный литерал", () => {
    expect(PLATFORM_VERSION_PATTERN.source).toBe("^\\d+\\.\\d+\\.\\d+\\.\\d+$");
  });

  it("max-длины полей == литералы спеки", () => {
    expect(NAME_MAX_LENGTH).toBe(200);
    expect(DATABASE_SERVER_MAX_LENGTH).toBe(200);
    expect(DATABASE_NAME_MAX_LENGTH).toBe(200);
    expect(SITE_NAME_MAX_LENGTH).toBe(200);
    expect(VIRTUAL_PATH_MAX_LENGTH).toBe(200);
    expect(PLATFORM_VERSION_MAX_LENGTH).toBe(50);
    expect(PHYSICAL_PATH_MAX_LENGTH).toBe(260);
  });
});

describe("validation — правила virtualPath (фиксация)", () => {
  const schema = buildInfobaseFormSchema((k) => k);

  const validPublication = {
    siteName: "Default Web Site",
    platformVersion: "8.3.23.1865",
  };
  const base = {
    tenantId: "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa",
    name: "База 1",
    clusterInfobaseId: "dddddddd-dddd-dddd-dddd-dddddddddddd",
    databaseServer: "sql-01",
    databaseName: "acme",
    status: "Active" as const,
  };

  function parseVirtualPath(virtualPath: string) {
    return schema.safeParse({
      ...base,
      publication: { ...validPublication, virtualPath },
    }).success;
  }

  it.each([
    ["/acme-bp", true],
    ["/acme", true],
    ["acme-bp", false], // нет ведущего «/»
    ["/acme bp", false], // пробел
    ["", false],
  ])("%s → %s", (virtualPath, expected) => {
    expect(parseVirtualPath(virtualPath as string)).toBe(expected);
  });
});
