import { describe, it, expect } from "vitest";
import { updateStatusSchema } from "../types";

// MLC-176 — parity BE↔FE. Когда проверка недоступна (checkAvailable=false), API
// ОПУСКАЕТ latestVersion/releaseUrl/downloadUrl (известный урок проекта про
// omit-null). Схема обязана парсить такой ответ без ошибки (поля `.nullish()`).
describe("updateStatusSchema (omit-null parity)", () => {
  it("парсит ответ БЕЗ опущенных nullable-полей (checkAvailable=false)", () => {
    const parsed = updateStatusSchema.parse({
      currentVersion: "0.7.0-beta",
      updateAvailable: false,
      checkAvailable: false,
      checkedAtUtc: "2026-06-16T10:00:00Z",
    });
    expect(parsed.currentVersion).toBe("0.7.0-beta");
    expect(parsed.checkAvailable).toBe(false);
    expect(parsed.latestVersion ?? null).toBeNull();
  });

  it("парсит полный ответ с обновлением (checkAvailable=true)", () => {
    const parsed = updateStatusSchema.parse({
      currentVersion: "0.7.0-beta",
      latestVersion: "0.8.0",
      updateAvailable: true,
      releaseUrl: "https://github.com/sdbsm/MitLicenseCenter/releases/tag/v0.8.0",
      downloadUrl: "https://example/Setup.exe",
      checkAvailable: true,
      checkedAtUtc: "2026-06-16T10:00:00Z",
    });
    expect(parsed.updateAvailable).toBe(true);
    expect(parsed.downloadUrl).toBe("https://example/Setup.exe");
  });

  it("отвергает ответ без обязательного currentVersion", () => {
    expect(() =>
      updateStatusSchema.parse({
        updateAvailable: false,
        checkAvailable: false,
        checkedAtUtc: "2026-06-16T10:00:00Z",
      })
    ).toThrow();
  });
});
