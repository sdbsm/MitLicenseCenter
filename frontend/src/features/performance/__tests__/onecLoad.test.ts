import { describe, expect, it } from "vitest";
import {
  availablePerformanceBand,
  classifySession,
  formatAvgCallMs,
  formatBytes,
  formatMs,
  formatSignedMb,
  shortUuid,
  sortSessionsByLoad,
  type SessionState,
} from "../onecLoad";
import type { OneCSessionLoad } from "../types";

const CAPTURED = "2026-06-08T12:00:00Z";

function session(o: Partial<OneCSessionLoad>): OneCSessionLoad {
  return {
    sessionId: "11111111-1111-1111-1111-111111111111",
    sessionNumber: 1,
    clusterInfobaseId: "22222222-2222-2222-2222-222222222222",
    appId: "1CV8C",
    userName: "Андрей",
    host: "WS01",
    process: "33333333-3333-3333-3333-333333333333",
    connection: "44444444-4444-4444-4444-444444444444",
    cpuTimeCurrent: 0,
    durationCurrent: 0,
    durationCurrentDbms: 0,
    memoryCurrent: 0,
    blockedByDbms: 0,
    blockedByLs: 0,
    lastActiveAtUtc: CAPTURED,
    ...o,
  };
}

describe("classifySession", () => {
  const cases: Array<[string, Partial<OneCSessionLoad>, SessionState]> = [
    [
      "заблокирован СУБД важнее долгого вызова",
      { blockedByDbms: 5, durationCurrent: 99_999 },
      "blocked",
    ],
    ["заблокирован управляемой блокировкой", { blockedByLs: 2 }, "blocked"],
    ["долгий текущий вызов", { durationCurrent: 6_000 }, "long"],
    ["активный короткий вызов", { durationCurrent: 500 }, "active"],
    [
      "молчит дольше порога без вызова",
      { durationCurrent: 0, lastActiveAtUtc: "2026-06-08T11:50:00Z" },
      "silent",
    ],
    ["простаивает (недавняя активность, нет вызова)", { durationCurrent: 0 }, "idle"],
  ];

  it.each(cases)("%s", (_label, overrides, expected) => {
    expect(classifySession(session(overrides), CAPTURED)).toBe(expected);
  });

  it("null-метрики не считаются блокировкой/нагрузкой", () => {
    expect(
      classifySession(
        session({ blockedByDbms: null, blockedByLs: null, durationCurrent: null }),
        CAPTURED
      )
    ).toBe("idle");
  });
});

describe("sortSessionsByLoad", () => {
  it("сортирует по cpu-time, при равенстве — по длительности; null тонут вниз", () => {
    const rows = sortSessionsByLoad([
      session({ sessionId: "a", cpuTimeCurrent: 10, durationCurrent: 1 }),
      session({ sessionId: "b", cpuTimeCurrent: 100 }),
      session({ sessionId: "c", cpuTimeCurrent: null }),
      session({ sessionId: "d", cpuTimeCurrent: 10, durationCurrent: 50 }),
    ]);
    expect(rows.map((r) => r.sessionId)).toEqual(["b", "d", "a", "c"]);
  });

  it("не мутирует исходный массив", () => {
    const input = [session({ sessionId: "x", cpuTimeCurrent: 1 })];
    sortSessionsByLoad(input);
    expect(input[0].sessionId).toBe("x");
  });
});

describe("availablePerformanceBand", () => {
  it.each([
    [null, null],
    [900, "ok"],
    [800, "ok"],
    [700, "warn"],
    [500, "warn"],
    [200, "crit"],
  ])("%s → %s", (value, band) => {
    expect(availablePerformanceBand(value as number | null)).toBe(band);
  });
});

describe("форматирование (null → «—», не 0)", () => {
  it("formatMs", () => {
    expect(formatMs(null)).toBe("—");
    expect(formatMs(0)).toBe("0 мс");
    expect(formatMs(450)).toBe("450 мс");
    expect(formatMs(1_500)).toBe("1.5 с");
  });

  it("formatSignedMb сохраняет отрицательный знак (GC)", () => {
    expect(formatSignedMb(null)).toBe("—");
    expect(formatSignedMb(-1_138_560)).toBe("−1.1 МБ");
    expect(formatSignedMb(2 * 1024 ** 2)).toBe("2.0 МБ");
  });

  it("formatBytes — МБ/ГБ", () => {
    expect(formatBytes(null)).toBe("—");
    expect(formatBytes(500 * 1024 ** 2)).toBe("500 МБ");
    expect(formatBytes(2 * 1024 ** 3)).toBe("2.0 ГБ");
  });

  it("formatAvgCallMs — секунды rac → мс", () => {
    expect(formatAvgCallMs(null)).toBe("—");
    expect(formatAvgCallMs(3.54)).toBe("3540 мс");
    expect(formatAvgCallMs(0.002)).toBe("2 мс");
  });

  it("shortUuid", () => {
    expect(shortUuid(null)).toBe("—");
    expect(shortUuid("487281d5-65b5-4d8a-ae39-b156b909fcaf")).toBe("487281d5");
  });
});
