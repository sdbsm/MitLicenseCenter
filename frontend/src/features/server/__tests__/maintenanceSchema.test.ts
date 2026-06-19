import { describe, it, expect } from "vitest";
import { backupFreshnessSchema } from "../useMaintenanceBackups";

// MLC-216: Zod-граница вкладки «Обслуживание». BE опускает null-поля
// (гоча api-omits-null-fields): lastFull/lastDiff/lastLog приходят со значением или ключ
// отсутствует — НИКОГДА явным null. omittable() парсит оба случая, нормализуя в null.
// status — z.string() ради forward-compat (новое значение не роняет границу).
describe("backupFreshnessSchema (MLC-216, omit-null граница)", () => {
  it("парсит ответ с ОПУЩЕННЫМИ датами (ключей нет)", () => {
    const wire = {
      status: "Ok",
      databases: [{ databaseName: "acme_bp", isStale: true }],
    };

    const parsed = backupFreshnessSchema.parse(wire);

    expect(parsed.status).toBe("Ok");
    expect(parsed.databases[0].databaseName).toBe("acme_bp");
    expect(parsed.databases[0].isStale).toBe(true);
    expect(parsed.databases[0].lastFullUtc).toBeNull();
    expect(parsed.databases[0].lastDiffUtc).toBeNull();
    expect(parsed.databases[0].lastLogUtc).toBeNull();
  });

  it("парсит ответ с присутствующими датами", () => {
    const wire = {
      status: "Ok",
      databases: [
        {
          databaseName: "acme_bp",
          lastFullUtc: "2026-06-19T01:00:00Z",
          lastDiffUtc: "2026-06-19T07:00:00Z",
          lastLogUtc: "2026-06-19T11:30:00Z",
          isStale: false,
        },
      ],
    };

    const parsed = backupFreshnessSchema.parse(wire);

    expect(parsed.databases[0].lastFullUtc).toBe("2026-06-19T01:00:00Z");
    expect(parsed.databases[0].isStale).toBe(false);
  });

  it("деградация: status PermissionDenied с пустым databases", () => {
    const parsed = backupFreshnessSchema.parse({ status: "PermissionDenied", databases: [] });
    expect(parsed.status).toBe("PermissionDenied");
    expect(parsed.databases).toEqual([]);
  });

  it("неизвестный будущий status не роняет границу (string)", () => {
    const parsed = backupFreshnessSchema.parse({ status: "SomeFutureStatus", databases: [] });
    expect(parsed.status).toBe("SomeFutureStatus");
  });
});
