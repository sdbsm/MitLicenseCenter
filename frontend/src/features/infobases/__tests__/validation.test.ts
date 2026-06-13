import { describe, it, expect } from "vitest";
import {
  PLATFORM_VERSION_PATTERN,
  NAME_MAX_LENGTH,
  DATABASE_NAME_MAX_LENGTH,
  SITE_NAME_MAX_LENGTH,
  VIRTUAL_PATH_MAX_LENGTH,
  PLATFORM_VERSION_MAX_LENGTH,
  PHYSICAL_PATH_MAX_LENGTH,
  isConnStrSafeName,
  isSafeDatabaseName,
  isSafeVirtualPath,
  isSafePhysicalPath,
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

// MLC-118 — golden-таблицы барьера валидации. Парные backend'у (InfobasesValidationTests.cs);
// проза-спека — docs/03_DOMAIN_MODEL.md (§1.1, §3.5). Бьют по экспортируемым предикатам и
// через buildInfobaseFormSchema(...).safeParse(...) — тот же единый источник, что и форма.
describe("validation — предикаты безопасности символов (парные backend'у)", () => {
  it.each([
    ["Acme", true],
    ["Acme;x", false],
    ["Acme=x", false],
    ['Acme"x', false],
  ])("isConnStrSafeName(%s) → %s", (value, expected) => {
    expect(isConnStrSafeName(value as string)).toBe(expected);
  });

  it.each([
    ["acme_bp", true],
    ["Acme.BP", true],
    ["a..b", false],
    ["a/b", false],
    ["a\\b", false],
    ["a:b", false],
    ["a*b", false],
    ["a?b", false],
    ['a"b', false],
    ["a<b", false],
    ["a>b", false],
    ["a|b", false],
    ["a;b", false],
    ["a'b", false],
    ["a[b]", false],
  ])("isSafeDatabaseName(%s) → %s", (value, expected) => {
    expect(isSafeDatabaseName(value as string)).toBe(expected);
  });

  it.each([
    ["/acme", true],
    ["/acme/sub", true],
    ["/a..b", false],
    ["/a\\b", false],
  ])("isSafeVirtualPath(%s) → %s", (value, expected) => {
    expect(isSafeVirtualPath(value as string)).toBe(expected);
  });

  it.each([
    ["C:\\pub\\app", true],
    ["\\\\server\\share\\app", true],
    ["C:\\pub\\..\\app", false],
    ["C:\\pub;x", false],
    ["C:\\pub=x", false],
    ['C:\\pub"x', false],
  ])("isSafePhysicalPath(%s) → %s", (value, expected) => {
    expect(isSafePhysicalPath(value as string)).toBe(expected);
  });
});

describe("validation — барьер на полях формы (safeParse)", () => {
  const schema = buildInfobaseFormSchema((k) => k);
  const validPublication = {
    siteName: "Default Web Site",
    virtualPath: "/acme-bp",
    platformVersion: "8.3.23.1865",
  };
  const base = {
    tenantId: "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa",
    clusterInfobaseId: "dddddddd-dddd-dddd-dddd-dddddddddddd",
    status: "Active" as const,
  };

  function parse(overrides: {
    name?: string;
    databaseName?: string;
    virtualPath?: string;
    platformVersion?: string;
    physicalPathOverride?: string;
  }) {
    return schema.safeParse({
      ...base,
      name: overrides.name ?? "База 1",
      databaseName: overrides.databaseName ?? "acme",
      publication: {
        ...validPublication,
        virtualPath: overrides.virtualPath ?? validPublication.virtualPath,
        platformVersion: overrides.platformVersion ?? validPublication.platformVersion,
        ...(overrides.physicalPathOverride !== undefined
          ? { physicalPathOverride: overrides.physicalPathOverride }
          : {}),
      },
    }).success;
  }

  it("connstr-метасимвол в name → ошибка", () => {
    expect(parse({ name: "Acme;Ref=evil" })).toBe(false);
  });
  it("name на пределе длины проходит, длиннее — ошибка", () => {
    expect(parse({ name: "a".repeat(NAME_MAX_LENGTH) })).toBe(true);
    expect(parse({ name: "a".repeat(NAME_MAX_LENGTH + 1) })).toBe(false);
  });
  it("path-метасимвол / traversal в databaseName → ошибка", () => {
    expect(parse({ databaseName: "a/b" })).toBe(false);
    expect(parse({ databaseName: "a..b" })).toBe(false);
    expect(parse({ databaseName: "a".repeat(DATABASE_NAME_MAX_LENGTH + 1) })).toBe(false);
  });
  it("backslash / traversal в virtualPath → ошибка", () => {
    expect(parse({ virtualPath: "/a\\b" })).toBe(false);
    expect(parse({ virtualPath: "/a..b" })).toBe(false);
    expect(parse({ virtualPath: "/" + "a".repeat(VIRTUAL_PATH_MAX_LENGTH) })).toBe(false);
  });
  it("длина platformVersion режется на форме", () => {
    expect(parse({ platformVersion: "8.3.23." + "1".repeat(PLATFORM_VERSION_MAX_LENGTH) })).toBe(
      false
    );
  });
  it('PhysicalPathOverride: абсолютный без «..»/«; = "» проходит, иначе ошибка', () => {
    expect(parse({ physicalPathOverride: "C:\\pub\\acme" })).toBe(true);
    expect(parse({ physicalPathOverride: "C:\\pub\\..\\acme" })).toBe(false);
    expect(parse({ physicalPathOverride: "C:\\pub;x" })).toBe(false);
    expect(parse({ physicalPathOverride: "C:\\" + "a".repeat(PHYSICAL_PATH_MAX_LENGTH) })).toBe(
      false
    );
  });
  it("полностью валидные поля проходят", () => {
    expect(parse({ physicalPathOverride: "C:\\pub\\acme" })).toBe(true);
  });
});
