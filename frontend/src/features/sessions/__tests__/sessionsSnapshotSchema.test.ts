import { describe, expect, it } from "vitest";
import { sessionsSnapshotResponseSchema } from "../types";

/**
 * FE-11 (MLC-120). Снимок активных сеансов — критичная Zod-граница (MLC-016): питает
 * операционную картину over-limit/kill. Тест реально ПРОГОНЯЕТ схему сырым ответом
 * «как с провода», а не мокает её мимо. ADR-48 (MLC-166): licenseStatus — enum
 * (Consuming/NotConsuming/Pending), licenseFactAvailable — bool с дефолтом false.
 */
describe("sessionsSnapshotResponseSchema", () => {
  const entry = {
    sessionId: "11111111-1111-1111-1111-111111111111",
    clusterInfobaseId: "22222222-2222-2222-2222-222222222222",
    tenantId: "33333333-3333-3333-3333-333333333333",
    tenantName: "Acme",
    infobaseName: "Acme BP",
    appId: "1CV8C",
    userName: "ivanov",
    host: "WS-01",
    licenseStatus: "Consuming",
    startedAt: "2026-06-13T08:00:00Z",
    durationSeconds: 1234,
  };

  it("принимает валидный снимок с одним сеансом", () => {
    const parsed = sessionsSnapshotResponseSchema.parse({
      items: [entry],
      capturedAt: "2026-06-13T08:01:00Z",
      tookMs: 12,
      source: "ras",
      licenseFactAvailable: true,
    });
    expect(parsed.items).toHaveLength(1);
    expect(parsed.items[0].licenseStatus).toBe("Consuming");
    expect(parsed.items[0].durationSeconds).toBe(1234);
    expect(parsed.licenseFactAvailable).toBe(true);
  });

  it("принимает пустой снимок (нет активных сеансов)", () => {
    const parsed = sessionsSnapshotResponseSchema.parse({
      items: [],
      capturedAt: "2026-06-13T08:01:00Z",
      tookMs: 3,
      source: "ras",
      licenseFactAvailable: false,
    });
    expect(parsed.items).toHaveLength(0);
  });

  it("licenseFactAvailable по умолчанию false при отсутствии ключа (parity-резерв)", () => {
    const parsed = sessionsSnapshotResponseSchema.parse({
      items: [],
      capturedAt: "2026-06-13T08:01:00Z",
      tookMs: 3,
      source: "ras",
    });
    expect(parsed.licenseFactAvailable).toBe(false);
  });

  it("отвергает запись с неверным значением licenseStatus", () => {
    expect(() =>
      sessionsSnapshotResponseSchema.parse({
        items: [{ ...entry, licenseStatus: "yes" }],
        capturedAt: "2026-06-13T08:01:00Z",
        tookMs: 3,
        source: "ras",
        licenseFactAvailable: true,
      })
    ).toThrow();
  });

  it("отвергает ответ без обязательного поля capturedAt", () => {
    expect(() =>
      sessionsSnapshotResponseSchema.parse({
        items: [],
        tookMs: 3,
        source: "ras",
      })
    ).toThrow();
  });
});
