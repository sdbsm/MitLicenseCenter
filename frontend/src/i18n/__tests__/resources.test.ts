import { describe, expect, it } from "vitest";
import { ru } from "@/i18n";

/**
 * Страховка целостности словаря после разнесения ru.json по per-feature файлам
 * (MLC-027). Словарь собирается из ./ru/<topLevelKey>.json в i18n/index.ts; этот
 * тест ловит потерю целого среза (забытый import/spread) или вложенного ключа при
 * будущих правках. Значения не проверяем — только наличие структуры.
 */

const TOP_LEVEL_KEYS = [
  "common",
  "nav",
  "theme",
  "table",
  "auth",
  "dashboard",
  "design",
  "profile",
  "tenants",
  "users",
  "infobases",
  "backups",
  "publications",
  "reports",
  "performance",
  "audit",
  "sessions",
  "settings",
  "discovery",
  "updates",
  "errors",
] as const;

describe("Сборка словаря i18n из per-feature файлов (MLC-027)", () => {
  it("содержит ровно все top-level срезы", () => {
    expect(Object.keys(ru).sort()).toEqual([...TOP_LEVEL_KEYS].sort());
  });

  it.each(TOP_LEVEL_KEYS)("срез %s — непустой объект", (key) => {
    const slice = (ru as Record<string, unknown>)[key];
    expect(slice, `срез ${key} отсутствует`).toBeTypeOf("object");
    expect(Object.keys(slice as object).length).toBeGreaterThan(0);
  });

  it("репрезентативные вложенные ключи на месте", () => {
    expect(ru.nav.version).toBeTruthy();
    expect(ru.common.notUpdatedYet).toBeTruthy();
    expect(ru.common.noData).toBeTruthy();
    expect(ru.audit.actions).toBeTypeOf("object");
  });
});
