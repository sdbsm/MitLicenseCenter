import { describe, expect, it } from "vitest";
import { sessionsSnapshotResponseSchema } from "../types";

/**
 * FE-11 (MLC-120). Снимок активных сеансов — критичная Zod-граница (MLC-016): питает
 * операционную картину over-limit/kill. Тест реально ПРОГОНЯЕТ схему сырым ответом
 * «как с провода», а не мокает её мимо. Все поля required (нет omit-null у этого
 * контракта), поэтому проверяем happy-path и отвержение неверного типа/неполного ответа.
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
    consumesLicense: true,
    startedAt: "2026-06-13T08:00:00Z",
    durationSeconds: 1234,
  };

  it("принимает валидный снимок с одним сеансом", () => {
    const parsed = sessionsSnapshotResponseSchema.parse({
      items: [entry],
      capturedAt: "2026-06-13T08:01:00Z",
      tookMs: 12,
      source: "ras",
    });
    expect(parsed.items).toHaveLength(1);
    expect(parsed.items[0].consumesLicense).toBe(true);
    expect(parsed.items[0].durationSeconds).toBe(1234);
  });

  it("принимает пустой снимок (нет активных сеансов)", () => {
    const parsed = sessionsSnapshotResponseSchema.parse({
      items: [],
      capturedAt: "2026-06-13T08:01:00Z",
      tookMs: 3,
      source: "ras",
    });
    expect(parsed.items).toHaveLength(0);
  });

  it("отвергает запись с неверным типом consumesLicense", () => {
    expect(() =>
      sessionsSnapshotResponseSchema.parse({
        items: [{ ...entry, consumesLicense: "yes" }],
        capturedAt: "2026-06-13T08:01:00Z",
        tookMs: 3,
        source: "ras",
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
