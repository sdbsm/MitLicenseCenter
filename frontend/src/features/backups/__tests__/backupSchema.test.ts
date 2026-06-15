import { describe, expect, it } from "vitest";
import { backupListSchema, backupSummarySchema } from "../types";

/**
 * Регрессия (урок [[api-omits-null-fields]], применён превентивно как в MLC-069/071):
 * бэкенд сериализует `null`-поля **пропуском ключа** (`JsonIgnoreCondition.WhenWritingNull`).
 * У Queued-строки `startedAtUtc`/`completedAtUtc`/`filePath`/`fileSizeBytes`/`errorMessage`
 * не приходят как `null`, а отсутствуют вовсе. Схема обязана это принимать (`omittable()`),
 * иначе Zod-граница отвергнет весь список и диалог упадёт в ошибку.
 */
describe("backupSummarySchema", () => {
  it("принимает Queued-строку ровно как с провода — все 5 nullable-полей опущены", () => {
    const raw = {
      id: "11111111-1111-1111-1111-111111111111",
      infobaseId: "22222222-2222-2222-2222-222222222222",
      databaseServer: "(local)",
      databaseName: "acme_bp",
      status: "Queued",
      requestedBy: "operator",
      requestedAtUtc: "2026-06-10T08:00:00Z",
      failureReason: "None",
    };
    const parsed = backupSummarySchema.parse(raw);
    expect(parsed.startedAtUtc).toBeNull();
    expect(parsed.completedAtUtc).toBeNull();
    expect(parsed.filePath).toBeNull();
    expect(parsed.fileSizeBytes).toBeNull();
    expect(parsed.errorMessage).toBeNull();
    expect(parsed.status).toBe("Queued");
  });

  it("принимает Succeeded-строку с заполненными полями", () => {
    const parsed = backupSummarySchema.parse({
      id: "33333333-3333-3333-3333-333333333333",
      infobaseId: "22222222-2222-2222-2222-222222222222",
      databaseServer: "(local)",
      databaseName: "acme_bp",
      status: "Succeeded",
      requestedBy: "operator",
      requestedAtUtc: "2026-06-10T08:00:00Z",
      startedAtUtc: "2026-06-10T08:00:05Z",
      completedAtUtc: "2026-06-10T08:01:10Z",
      filePath: "D:\\Backups\\acme_bp\\acme_bp_20260610_080005.bak",
      fileSizeBytes: 123_456_789,
      failureReason: "None",
    });
    expect(parsed.fileSizeBytes).toBe(123_456_789);
    expect(parsed.completedAtUtc).toBe("2026-06-10T08:01:10Z");
  });

  it("принимает Failed-строку с причиной и сообщением", () => {
    const parsed = backupSummarySchema.parse({
      id: "44444444-4444-4444-4444-444444444444",
      infobaseId: "22222222-2222-2222-2222-222222222222",
      databaseServer: "(local)",
      databaseName: "acme_bp",
      status: "Failed",
      requestedBy: "operator",
      requestedAtUtc: "2026-06-10T08:00:00Z",
      startedAtUtc: "2026-06-10T08:00:05Z",
      failureReason: "InsufficientSpace",
      errorMessage: "Свободно 1024 МБ, требуется 4096 МБ.",
    });
    expect(parsed.failureReason).toBe("InsufficientSpace");
    expect(parsed.errorMessage).toContain("Свободно");
  });

  it("незнакомый статус/причина будущего бэкенда НЕ роняют список (деградация, не отказ)", () => {
    const parsed = backupListSchema.parse([
      {
        id: "55555555-5555-5555-5555-555555555555",
        infobaseId: "22222222-2222-2222-2222-222222222222",
        databaseServer: "(local)",
        databaseName: "acme_bp",
        status: "Archived",
        requestedBy: "operator",
        requestedAtUtc: "2026-06-10T08:00:00Z",
        failureReason: "SomethingNew",
      },
    ]);
    expect(parsed).toHaveLength(1);
    expect(parsed[0].status).toBe("Archived");
    expect(parsed[0].failureReason).toBe("SomethingNew");
  });

  it("не-строковый статус по-прежнему отвергается", () => {
    expect(() =>
      backupSummarySchema.parse({
        id: "66666666-6666-6666-6666-666666666666",
        infobaseId: "22222222-2222-2222-2222-222222222222",
        databaseServer: "(local)",
        databaseName: "acme_bp",
        status: 3,
        requestedBy: "operator",
        requestedAtUtc: "2026-06-10T08:00:00Z",
        failureReason: "None",
      })
    ).toThrow();
  });
});
