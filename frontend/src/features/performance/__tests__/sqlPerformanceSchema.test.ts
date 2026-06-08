import { describe, expect, it } from "vitest";
import { sqlPerformanceViewSchema } from "../types";

/**
 * Регрессия по уроку MLC-067 (api-omits-null-fields): бэкенд сериализует `null`-поля
 * **пропуском ключа** (`JsonIgnoreCondition.WhenWritingNull`) — у активного запроса
 * `blockingSessionId`/`databaseName`/`waitType`/… приходят не как `null`, а отсутствуют.
 * Схема обязана это принимать (`omittable` = `.nullish()` + нормализация в `null`), иначе
 * Zod-граница отвергает весь снимок. Тесты намеренно собраны на СЫРОМ ответе с опущенными
 * ключами, а не на полном объекте, — иначе дефект не ловится.
 */
describe("sqlPerformanceViewSchema", () => {
  it("принимает активный запрос с опущенными null-ключами и нормализует их в null", () => {
    const raw = {
      snapshot: {
        capturedAtUtc: "2026-06-09T10:00:00Z",
        status: "Ok",
        measuring: false,
        activeRequests: [
          {
            // ровно как отдаёт бэкенд: blocking/database/program/host/wait*/cpu/elapsed/reads/text опущены
            sessionId: 77,
            isOneC: true,
            status: "running",
          },
        ],
        databaseIo: [],
        topWaits: [],
      },
      // атрибуция с опущенными tenant-полями (системная/незарегистрированная база)
      databases: [{ databaseName: "master" }],
    };

    const parsed = sqlPerformanceViewSchema.parse(raw);
    const req = parsed.snapshot.activeRequests[0];
    expect(req.blockingSessionId).toBeNull();
    expect(req.databaseName).toBeNull();
    expect(req.waitType).toBeNull();
    expect(req.cpuTimeMs).toBeNull();
    expect(req.sqlText).toBeNull();
    expect(req.isOneC).toBe(true);

    const db = parsed.databases[0];
    expect(db.databaseName).toBe("master");
    expect(db.tenantId).toBeNull();
    expect(db.tenantName).toBeNull();
    expect(db.infobaseName).toBeNull();
  });

  it("принимает явный null наравне с отсутствием ключа", () => {
    const parsed = sqlPerformanceViewSchema.parse({
      snapshot: {
        capturedAtUtc: "2026-06-09T10:00:00Z",
        status: "Ok",
        measuring: false,
        activeRequests: [
          {
            sessionId: 80,
            blockingSessionId: null,
            databaseName: "mitpro",
            isOneC: false,
            programName: null,
            hostName: null,
            status: "suspended",
            waitType: "LCK_M_X",
            waitTimeMs: 1500,
            cpuTimeMs: 12,
            elapsedMs: 3400,
            logicalReads: 2048,
            sqlText: null,
          },
        ],
        databaseIo: [{ readStallMsDelta: 5, writeStallMsDelta: 0, readsDelta: 12, writesDelta: 0 }],
        topWaits: [{ waitType: "LCK_M_X", waitTimeMsDelta: 1500, waitingTasksDelta: 1 }],
      },
      databases: [
        {
          databaseName: "mitpro",
          tenantId: "t1",
          tenantName: "Клиент",
          infobaseName: "Бухгалтерия",
        },
      ],
    });
    const req = parsed.snapshot.activeRequests[0];
    expect(req.sqlText).toBeNull();
    expect(req.waitType).toBe("LCK_M_X");
    expect(parsed.snapshot.databaseIo[0].databaseName).toBeNull();
  });

  it("принимает degraded-снимок (PermissionDenied) с пустыми списками", () => {
    const parsed = sqlPerformanceViewSchema.parse({
      snapshot: {
        capturedAtUtc: "2026-06-09T10:00:00Z",
        status: "PermissionDenied",
        measuring: false,
        activeRequests: [],
        databaseIo: [],
        topWaits: [],
      },
      databases: [],
    });
    expect(parsed.snapshot.status).toBe("PermissionDenied");
  });

  it("отвергает неизвестный статус (enum-граница)", () => {
    const build = (status: string) => ({
      snapshot: {
        capturedAtUtc: "2026-06-09T10:00:00Z",
        status,
        measuring: false,
        activeRequests: [],
        databaseIo: [],
        topWaits: [],
      },
      databases: [],
    });
    expect(() => sqlPerformanceViewSchema.parse(build("Broken"))).toThrow();
  });
});
