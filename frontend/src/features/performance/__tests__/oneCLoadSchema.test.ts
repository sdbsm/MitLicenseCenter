import { describe, expect, it } from "vitest";
import { oneCLoadSnapshotSchema } from "../types";

/**
 * Регрессия (найдена в preview MLC-067): бэкенд сериализует `null`-поля **пропуском
 * ключа** (`JsonIgnoreCondition.WhenWritingNull`) — у idle-сеанса `process`/`connection`
 * не приходят как `null`, а отсутствуют. Схема обязана это принимать (`.nullish()` +
 * нормализация в `null`), иначе Zod-граница отвергает весь снимок и секция падает в ошибку.
 */
describe("oneCLoadSnapshotSchema", () => {
  it("принимает сеанс с пропущенными null-ключами и нормализует их в null", () => {
    const raw = {
      capturedAtUtc: "2026-06-08T20:19:12Z",
      sessions: [
        {
          // idle-сеанс ровно как отдаёт бэкенд: process/connection отсутствуют
          sessionId: "02d5184c-65b5-4d8a-ae39-b156b909fcaf",
          sessionNumber: 1,
          clusterInfobaseId: "6256b6f3-dde1-41f9-a6c2-bdfc36bca7aa",
          appId: "1CV8C",
          userName: "Андрей",
          host: "Andrey-pc",
          cpuTimeCurrent: 0,
          durationCurrent: 0,
          durationCurrentDbms: 0,
          memoryCurrent: 0,
          blockedByDbms: 0,
          blockedByLs: 0,
          lastActiveAtUtc: "2026-06-08T20:19:12Z",
        },
      ],
      processes: [{ process: "487281d5-679e-422f-87fa-eb1224c030c5", pid: 15876 }],
    };

    const parsed = oneCLoadSnapshotSchema.parse(raw);
    expect(parsed.sessions[0].process).toBeNull();
    expect(parsed.sessions[0].connection).toBeNull();
    // присутствующий явный null тоже допустим
    expect(parsed.processes[0].availablePerformance).toBeNull();
    expect(parsed.processes[0].avgCallTime).toBeNull();
    expect(parsed.processes[0].memorySize).toBeNull();
  });

  it("принимает явный null наравне с отсутствием ключа", () => {
    const parsed = oneCLoadSnapshotSchema.parse({
      capturedAtUtc: "2026-06-08T20:19:12Z",
      sessions: [],
      processes: [
        {
          process: "487281d5-679e-422f-87fa-eb1224c030c5",
          pid: null,
          availablePerformance: 384,
          avgCallTime: 5.883,
          memorySize: 1_966_768,
        },
      ],
    });
    expect(parsed.processes[0].pid).toBeNull();
    expect(parsed.processes[0].availablePerformance).toBe(384);
  });
});
