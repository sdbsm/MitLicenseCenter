import { describe, it, expect } from "vitest";
import { isFilterBeyondRetention, retentionCutoffDate } from "../retention";

const NOW = new Date("2026-05-23T12:00:00Z");

describe("isFilterBeyondRetention", () => {
  it("returns false when fromYmd is empty", () => {
    expect(isFilterBeyondRetention(null, 365, NOW)).toBe(false);
    expect(isFilterBeyondRetention("", 365, NOW)).toBe(false);
  });

  it("returns false when retentionDays is missing or non-positive", () => {
    expect(isFilterBeyondRetention("2024-01-01", null, NOW)).toBe(false);
    expect(isFilterBeyondRetention("2024-01-01", undefined, NOW)).toBe(false);
    expect(isFilterBeyondRetention("2024-01-01", 0, NOW)).toBe(false);
    expect(isFilterBeyondRetention("2024-01-01", -30, NOW)).toBe(false);
  });

  it("returns false for malformed YYYY-MM-DD", () => {
    expect(isFilterBeyondRetention("not-a-date", 365, NOW)).toBe(false);
    expect(isFilterBeyondRetention("2026-13-99", 365, NOW)).toBe(false);
  });

  it("returns true when from is deeper than retention window", () => {
    // NOW=2026-05-23, retention=365d → cutoff=2025-05-23
    expect(isFilterBeyondRetention("2024-01-01", 365, NOW)).toBe(true);
  });

  it("returns false when from is within retention window", () => {
    expect(isFilterBeyondRetention("2026-05-20", 365, NOW)).toBe(false);
  });

  it("returns false at exact cutoff boundary (strict <)", () => {
    // cutoff = 2025-05-23 12:00Z; fromYmd "2025-05-23" parsed to 00:00Z (earlier) → true
    // Boundary test уточняем: cutoff с NOW=12:00Z minus 365d == 2025-05-23T12:00Z
    // "2025-05-23T00:00:00Z" < cutoff (12:00) → true; точная-в-точку проверка ниже.
    const NOW_MIDNIGHT = new Date("2026-05-23T00:00:00Z");
    expect(isFilterBeyondRetention("2025-05-23", 365, NOW_MIDNIGHT)).toBe(false);
  });
});

describe("retentionCutoffDate", () => {
  it("returns null when retentionDays missing", () => {
    expect(retentionCutoffDate(null, NOW)).toBeNull();
    expect(retentionCutoffDate(undefined, NOW)).toBeNull();
    expect(retentionCutoffDate(0, NOW)).toBeNull();
  });

  it("subtracts retentionDays from nowUtc", () => {
    const result = retentionCutoffDate(365, NOW);
    expect(result?.toISOString()).toBe("2025-05-23T12:00:00.000Z");
  });
});
