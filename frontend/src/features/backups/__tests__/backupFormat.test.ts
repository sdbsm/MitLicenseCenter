import { describe, expect, it } from "vitest";
import { backupStatusVariant, formatBackupSize } from "../backupFormat";
import type { BackupStatus } from "../types";

describe("backupStatusVariant", () => {
  it.each([
    ["Queued", "neutral"],
    ["Running", "info"],
    ["Succeeded", "success"],
    ["Failed", "danger"],
  ] as const)("%s → %s", (status, variant) => {
    expect(backupStatusVariant(status)).toBe(variant);
  });

  it("незнакомый статус деградирует к neutral, не падает", () => {
    expect(backupStatusVariant("Archived" as BackupStatus)).toBe("neutral");
  });
});

describe("formatBackupSize", () => {
  it("null → «—» (размер есть только у Succeeded)", () => {
    expect(formatBackupSize(null)).toBe("—");
  });

  it("малый файл — КБ", () => {
    expect(formatBackupSize(512_000)).toBe("500 КБ");
  });

  it("средний файл — МБ", () => {
    expect(formatBackupSize(123_456_789)).toBe("117.7 МБ");
  });

  it("большой файл — ГБ", () => {
    expect(formatBackupSize(5.5 * 1024 ** 3)).toBe("5.5 ГБ");
  });
});
