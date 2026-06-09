import { describe, expect, it } from "vitest";
import {
  aggregateOneCProcesses,
  aggregateOneCSessions,
  aggregateSqlRequests,
  hasOneCData,
  hasSqlData,
  lastOneCCapturedAt,
  toHostChartRows,
} from "../recordingAggregation";
import type { RecordingSample } from "../types";

function hostSample(over: Partial<RecordingSample>): RecordingSample {
  return {
    sampleUtc: "2026-06-09T10:00:00Z",
    measuring: false,
    cpuPercent: 0,
    cpuQueueLength: 0,
    memoryAvailableMBytes: 8192,
    memoryTotalMBytes: 16384,
    memoryPagesPerSec: 0,
    diskAvgReadSecPerOp: 0,
    diskAvgWriteSecPerOp: 0,
    diskQueueLength: 0,
    processesInaccessible: 0,
    processGroups: [],
    oneC: null,
    sql: null,
    ...over,
  };
}

describe("toHostChartRows", () => {
  it("выводит занятость памяти в % и худшую латентность диска в мс", () => {
    const rows = toHostChartRows([
      hostSample({
        cpuPercent: 42.55,
        memoryAvailableMBytes: 4096,
        memoryTotalMBytes: 16384,
        diskAvgReadSecPerOp: 0.002,
        diskAvgWriteSecPerOp: 0.005,
      }),
    ]);
    expect(rows[0].cpuPercent).toBe(42.6);
    // (16384-4096)/16384 = 75%
    expect(rows[0].memoryUsedPercent).toBe(75);
    // worst of read/write = 0.005с → 5 мс
    expect(rows[0].diskLatencyMs).toBe(5);
  });

  it("клампит занятость памяти в [0,100] и переносит measuring", () => {
    const rows = toHostChartRows([
      hostSample({ memoryAvailableMBytes: 0, memoryTotalMBytes: 0, measuring: true }),
    ]);
    expect(rows[0].memoryUsedPercent).toBe(0);
    expect(rows[0].measuring).toBe(true);
  });
});

describe("aggregateOneCSessions", () => {
  it("берёт пиковый по cpu-time-current срез каждого сеанса по всем сэмплам", () => {
    const mk = (cpu: number) => ({
      sessionId: "s1",
      sessionNumber: 1,
      clusterInfobaseId: "ib",
      appId: "1CV8C",
      userName: "u",
      host: "h",
      process: null,
      connection: null,
      cpuTimeCurrent: cpu,
      durationCurrent: cpu,
      durationCurrentDbms: 0,
      memoryCurrent: 0,
      blockedByDbms: 0,
      blockedByLs: 0,
      lastActiveAtUtc: null,
    });
    const samples = [
      hostSample({ oneC: { capturedAtUtc: "t1", sessions: [mk(100)], processes: [] } }),
      hostSample({ oneC: { capturedAtUtc: "t2", sessions: [mk(500)], processes: [] } }),
      hostSample({ oneC: { capturedAtUtc: "t3", sessions: [mk(200)], processes: [] } }),
    ];
    const result = aggregateOneCSessions(samples);
    expect(result).toHaveLength(1);
    expect(result[0].cpuTimeCurrent).toBe(500);
  });
});

describe("aggregateOneCProcesses", () => {
  it("берёт худший (минимальный available-perfomance) срез процесса", () => {
    const mk = (perf: number | null) => ({
      process: "p1",
      pid: 1,
      availablePerformance: perf,
      avgCallTime: 1,
      memorySize: 100,
    });
    const samples = [
      hostSample({ oneC: { capturedAtUtc: "t1", sessions: [], processes: [mk(800)] } }),
      hostSample({ oneC: { capturedAtUtc: "t2", sessions: [], processes: [mk(400)] } }),
      hostSample({ oneC: { capturedAtUtc: "t3", sessions: [], processes: [mk(null)] } }),
    ];
    const result = aggregateOneCProcesses(samples);
    expect(result).toHaveLength(1);
    // null не должен вытеснить реальный минимум 400
    expect(result[0].availablePerformance).toBe(400);
  });
});

describe("aggregateSqlRequests", () => {
  it("берёт пиковый по cpu-time срез каждого сеанса SQL", () => {
    const mk = (cpu: number) => ({
      sessionId: 77,
      blockingSessionId: null,
      databaseName: "mitpro",
      isOneC: true,
      programName: "1CV83 Server",
      hostName: "h",
      status: "running",
      waitType: null,
      waitTimeMs: null,
      cpuTimeMs: cpu,
      elapsedMs: cpu,
      logicalReads: 10,
      sqlText: "SELECT 1",
    });
    const samples = [
      hostSample({
        sql: {
          capturedAtUtc: "t1",
          status: "Ok",
          measuring: false,
          activeRequests: [mk(50)],
          databaseIo: [],
          topWaits: [],
        },
      }),
      hostSample({
        sql: {
          capturedAtUtc: "t2",
          status: "Ok",
          measuring: false,
          activeRequests: [mk(300)],
          databaseIo: [],
          topWaits: [],
        },
      }),
    ];
    const result = aggregateSqlRequests(samples);
    expect(result).toHaveLength(1);
    expect(result[0].cpuTimeMs).toBe(300);
  });
});

describe("hasOneCData / hasSqlData / lastOneCCapturedAt", () => {
  it("распознаёт наличие данных и берёт последний captured 1С", () => {
    const empty = [hostSample({}), hostSample({})];
    expect(hasOneCData(empty)).toBe(false);
    expect(hasSqlData(empty)).toBe(false);
    expect(lastOneCCapturedAt(empty)).toBeNull();

    const withData = [
      hostSample({
        oneC: {
          capturedAtUtc: "t-old",
          sessions: [
            {
              sessionId: "s",
              sessionNumber: 1,
              clusterInfobaseId: "ib",
              appId: "a",
              userName: "u",
              host: "h",
              process: null,
              connection: null,
              cpuTimeCurrent: 1,
              durationCurrent: 1,
              durationCurrentDbms: 0,
              memoryCurrent: 0,
              blockedByDbms: 0,
              blockedByLs: 0,
              lastActiveAtUtc: null,
            },
          ],
          processes: [],
        },
      }),
      hostSample({ oneC: { capturedAtUtc: "t-new", sessions: [], processes: [] } }),
    ];
    expect(hasOneCData(withData)).toBe(true);
    expect(lastOneCCapturedAt(withData)).toBe("t-new");
  });
});
