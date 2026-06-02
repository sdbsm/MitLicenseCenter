import { describe, it, expect } from "vitest";
import { z } from "zod";
import { pagedResponseSchema } from "../apiSchema";
import { currentUserSchema } from "@/features/auth/types";
import { sessionsSnapshotResponseSchema } from "@/features/sessions/types";

describe("pagedResponseSchema()", () => {
  const schema = pagedResponseSchema(z.object({ id: z.string() }));

  it("принимает валидный конверт {items,total,page,pageSize}", () => {
    const parsed = schema.parse({
      items: [{ id: "a" }, { id: "b" }],
      total: 2,
      page: 1,
      pageSize: 25,
    });
    expect(parsed.items).toHaveLength(2);
    expect(parsed.total).toBe(2);
  });

  it("отклоняет элемент неверной формы", () => {
    expect(() => schema.parse({ items: [{ id: 1 }], total: 1, page: 1, pageSize: 25 })).toThrow();
  });

  it("отклоняет конверт без total", () => {
    expect(() => schema.parse({ items: [], page: 1, pageSize: 25 })).toThrow();
  });
});

describe("currentUserSchema (критичная граница ролей)", () => {
  it("принимает валидного пользователя", () => {
    expect(currentUserSchema.parse({ userName: "admin", roles: ["Admin"] })).toEqual({
      userName: "admin",
      roles: ["Admin"],
    });
  });

  it("отклоняет roles неверного типа (риск ошибки авторизации)", () => {
    expect(() => currentUserSchema.parse({ userName: "admin", roles: "Admin" })).toThrow();
  });
});

describe("sessionsSnapshotResponseSchema", () => {
  it("принимает валидный снимок", () => {
    const parsed = sessionsSnapshotResponseSchema.parse({
      items: [
        {
          sessionId: "s1",
          clusterInfobaseId: "c1",
          tenantId: "t1",
          tenantName: "ООО Ромашка",
          infobaseName: "base",
          appId: "1CV8C",
          userName: "user",
          host: "PC-1",
          consumesLicense: true,
          startedAt: "2026-06-03T10:00:00Z",
          durationSeconds: 120,
        },
      ],
      capturedAt: "2026-06-03T10:05:00Z",
      tookMs: 42,
      source: "rac",
    });
    expect(parsed.items[0].consumesLicense).toBe(true);
  });

  it("отклоняет consumesLicense неверного типа", () => {
    expect(() =>
      sessionsSnapshotResponseSchema.parse({
        items: [{ consumesLicense: "yes" }],
        capturedAt: "x",
        tookMs: 0,
        source: "rac",
      })
    ).toThrow();
  });
});
