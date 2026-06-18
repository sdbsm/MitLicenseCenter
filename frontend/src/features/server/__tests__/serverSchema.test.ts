import { describe, it, expect } from "vitest";
import { serverStatusSchema, serverOperationSchema } from "../useServerStatus";

// BE опускает null-поля (гоча api-omits-null-fields): platformVersion / serviceName /
// instance / error приходят либо со значением, либо ключ отсутствует — НИКОГДА null.
// Схемы используют omittable() → парсятся в обоих случаях, нормализуя в null.
describe("serverStatusSchema (MLC-214, omit-null граница)", () => {
  it("парсит ответ с ОПУЩЕННЫМИ null-полями (ключей нет)", () => {
    const wire = {
      oneCServers: [{ serviceName: "1C:Enterprise 8.3 Server Agent", running: true }],
      ras: { state: "Ok", running: true, available: true },
      sql: { running: true, available: true },
      iis: { state: "Started", available: true },
      overall: "Healthy",
    };

    const parsed = serverStatusSchema.parse(wire);

    expect(parsed.oneCServers[0].platformVersion).toBeNull();
    expect(parsed.ras.serviceName).toBeNull();
    expect(parsed.ras.error).toBeNull();
    expect(parsed.sql.instance).toBeNull();
    expect(parsed.sql.serviceName).toBeNull();
    expect(parsed.iis.error).toBeNull();
    expect(parsed.overall).toBe("Healthy");
  });

  it("парсит ответ с присутствующими nullable-полями", () => {
    const wire = {
      oneCServers: [{ serviceName: "ragent", running: false, platformVersion: "8.3.23.1865" }],
      ras: { state: "Stopped", running: false, serviceName: "RAS1C", available: true, error: null },
      sql: {
        instance: "MSSQLSERVER",
        serviceName: "MSSQLSERVER",
        running: false,
        available: false,
        error: "Служба SQL недоступна.",
      },
      iis: { state: "Stopped", available: true, error: null },
      overall: "Degraded",
    };

    const parsed = serverStatusSchema.parse(wire);

    expect(parsed.oneCServers[0].platformVersion).toBe("8.3.23.1865");
    expect(parsed.ras.serviceName).toBe("RAS1C");
    expect(parsed.sql.instance).toBe("MSSQLSERVER");
    expect(parsed.sql.error).toBe("Служба SQL недоступна.");
    expect(parsed.overall).toBe("Degraded");
  });

  it("неизвестное будущее overall не роняет границу (string)", () => {
    const wire = {
      oneCServers: [],
      ras: { state: "Future", running: true, available: true },
      sql: { running: true, available: true },
      iis: { state: "Future", available: true },
      overall: "SomeFutureState",
    };

    const parsed = serverStatusSchema.parse(wire);
    expect(parsed.overall).toBe("SomeFutureState");
    expect(parsed.oneCServers).toEqual([]);
  });

  it("serverOperationSchema парсит ответ мутации", () => {
    const parsed = serverOperationSchema.parse({ serviceName: "ragent", finalStatus: "Running" });
    expect(parsed.finalStatus).toBe("Running");
  });
});
