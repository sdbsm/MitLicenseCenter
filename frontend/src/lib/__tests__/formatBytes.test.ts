import { describe, expect, it } from "vitest";
import { formatBytes } from "../formatBytes";

/**
 * MLC-185d. Единый форматтер размера КБ/МБ/ГБ (база 1024) — конвенция приложения,
 * на него делегирует formatBackupSize и колонка «Размер» баз. Проверяем границы единиц.
 */
describe("formatBytes", () => {
  it("малое значение — КБ (целое)", () => {
    expect(formatBytes(512_000)).toBe("500 КБ");
  });

  it("на границе МБ", () => {
    expect(formatBytes(1024 ** 2)).toBe("1.0 МБ");
  });

  it("среднее значение — МБ (один знак)", () => {
    expect(formatBytes(123_456_789)).toBe("117.7 МБ");
  });

  it("на границе ГБ", () => {
    expect(formatBytes(1024 ** 3)).toBe("1.0 ГБ");
  });

  it("большое значение — ГБ (один знак)", () => {
    expect(formatBytes(5.5 * 1024 ** 3)).toBe("5.5 ГБ");
  });

  it("ноль байт — 0 КБ", () => {
    expect(formatBytes(0)).toBe("0 КБ");
  });
});
