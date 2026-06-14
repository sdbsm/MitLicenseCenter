import { describe, expect, it } from "vitest";
import { healthSchema } from "../types";

/**
 * MLC-149. Wire-fixture для liveness `/api/v1/health` — сырой ответ «как с провода»
 * (см. backend HealthEndpoints.cs: { status, version, utcNow }) прогоняется через
 * Zod-границу. Версия в подвале сайдбара читается из этого ответа.
 */
describe("healthSchema", () => {
  it("принимает контракт /api/v1/health (status, version, utcNow)", () => {
    const parsed = healthSchema.parse({
      status: "ok",
      version: "0.4.0-beta",
      utcNow: "2026-06-14T08:00:00Z",
    });

    expect(parsed.version).toBe("0.4.0-beta");
    expect(parsed.status).toBe("ok");
  });

  it("незнакомое доп.поле будущего бэкенда НЕ роняет парс (толерантность)", () => {
    const parsed = healthSchema.parse({
      status: "ok",
      version: "0.5.0",
      utcNow: "2026-06-14T08:00:00Z",
      futureField: 42,
    });

    expect(parsed.version).toBe("0.5.0");
  });

  it("отсутствие version отвергается (граница строга к контракту)", () => {
    expect(() => healthSchema.parse({ status: "ok", utcNow: "2026-06-14T08:00:00Z" })).toThrow();
  });
});
