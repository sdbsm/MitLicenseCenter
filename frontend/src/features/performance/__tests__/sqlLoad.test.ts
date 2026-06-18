import { describe, expect, it } from "vitest";
import {
  aggregateByDatabase,
  attributionFor,
  buildAttributionMap,
  collectBlockerIds,
  formatInt,
  isBlocked,
  lockChainRows,
  sortRequestsByCpu,
  waitCategory,
} from "../sqlLoad";
import type { SqlActiveRequest, SqlDatabaseAttribution } from "../types";

function request(o: Partial<SqlActiveRequest>): SqlActiveRequest {
  return {
    sessionId: 1,
    blockingSessionId: null,
    databaseName: "mitpro",
    isOneC: true,
    programName: "1CV83 Server",
    hostName: "HOST-01",
    status: "running",
    waitType: null,
    waitTimeMs: null,
    cpuTimeMs: 0,
    elapsedMs: 0,
    logicalReads: null,
    sqlText: "SELECT 1",
    ...o,
  };
}

const databases: SqlDatabaseAttribution[] = [
  {
    databaseName: "mitpro",
    tenantId: "t1",
    tenantName: "ООО Ромашка",
    infobaseName: "Бухгалтерия",
  },
  { databaseName: "test", tenantId: null, tenantName: null, infobaseName: null },
];

describe("buildAttributionMap / attributionFor", () => {
  const map = buildAttributionMap(databases);

  it("сопоставляет имя базы без учёта регистра", () => {
    expect(attributionFor("MITPRO", map)?.tenantName).toBe("ООО Ромашка");
    expect(attributionFor("  mitpro  ", map)?.infobaseName).toBe("Бухгалтерия");
  });

  it("незарегистрированная/системная база → null", () => {
    expect(attributionFor("master", map)).toBeNull();
    expect(attributionFor(null, map)).toBeNull();
  });
});

describe("aggregateByDatabase", () => {
  const map = buildAttributionMap(databases);

  it("суммирует ЦП и считает запросы (в т.ч. 1С) по базе, сортирует по ЦП вниз", () => {
    const rows = aggregateByDatabase(
      [
        request({ databaseName: "test", isOneC: false, cpuTimeMs: 50 }),
        request({ databaseName: "mitpro", isOneC: true, cpuTimeMs: 100 }),
        request({ databaseName: "mitpro", isOneC: true, cpuTimeMs: 200 }),
        request({ databaseName: "mitpro", isOneC: false, cpuTimeMs: null }),
      ],
      map
    );
    expect(rows.map((r) => r.databaseName)).toEqual(["mitpro", "test"]);
    const mitpro = rows[0];
    expect(mitpro.requestCount).toBe(3);
    expect(mitpro.oneCRequestCount).toBe(2);
    expect(mitpro.cpuTimeMs).toBe(300);
    expect(mitpro.attribution?.tenantName).toBe("ООО Ромашка");
  });

  it("запросы без имени базы в сводку не попадают", () => {
    const rows = aggregateByDatabase([request({ databaseName: null })], map);
    expect(rows).toEqual([]);
  });

  it("база без единой ЦП-метрики → cpuTimeMs=null (не 0)", () => {
    const rows = aggregateByDatabase([request({ databaseName: "test", cpuTimeMs: null })], map);
    expect(rows[0].cpuTimeMs).toBeNull();
  });
});

describe("sortRequestsByCpu", () => {
  it("по ЦП, при равенстве — по длительности; null тонут; не мутирует вход", () => {
    const input = [
      request({ sessionId: 1, cpuTimeMs: 10, elapsedMs: 1 }),
      request({ sessionId: 2, cpuTimeMs: 100 }),
      request({ sessionId: 3, cpuTimeMs: null }),
      request({ sessionId: 4, cpuTimeMs: 10, elapsedMs: 50 }),
    ];
    expect(sortRequestsByCpu(input).map((r) => r.sessionId)).toEqual([2, 4, 1, 3]);
    expect(input[0].sessionId).toBe(1);
  });
});

describe("блокировки", () => {
  it("isBlocked — по наличию blockingSessionId", () => {
    expect(isBlocked(request({ blockingSessionId: 77 }))).toBe(true);
    expect(isBlocked(request({ blockingSessionId: null }))).toBe(false);
  });

  it("collectBlockerIds — множество сеансов-блокировщиков (цепочка)", () => {
    const ids = collectBlockerIds([
      request({ sessionId: 10, blockingSessionId: 20 }),
      request({ sessionId: 11, blockingSessionId: 20 }),
      request({ sessionId: 20, blockingSessionId: null }),
    ]);
    expect([...ids]).toEqual([20]);
  });
});

describe("lockChainRows", () => {
  it("берёт только заблокированные; флаг blocks — для тех, кто и сам блокирует (середина цепочки)", () => {
    // Цепочка A(10) ждёт B(20), B(20) ждёт C(30): B — заблокирован И блокирует.
    const rows = lockChainRows([
      request({ sessionId: 10, blockingSessionId: 20, elapsedMs: 100 }),
      request({ sessionId: 20, blockingSessionId: 30, elapsedMs: 50 }),
      request({ sessionId: 30, blockingSessionId: null, elapsedMs: 999 }),
    ]);
    expect(rows.map((r) => r.request.sessionId)).toEqual([10, 20]);
    expect(rows.find((r) => r.request.sessionId === 20)?.blocks).toBe(true);
    expect(rows.find((r) => r.request.sessionId === 10)?.blocks).toBe(false);
  });

  it("сортирует по длительности ожидания вниз, null тонут; не мутирует вход", () => {
    const input = [
      request({ sessionId: 1, blockingSessionId: 9, elapsedMs: 10 }),
      request({ sessionId: 2, blockingSessionId: 9, elapsedMs: null }),
      request({ sessionId: 3, blockingSessionId: 9, elapsedMs: 500 }),
    ];
    expect(lockChainRows(input).map((r) => r.request.sessionId)).toEqual([3, 1, 2]);
    expect(input[0].sessionId).toBe(1);
  });

  it("нет заблокированных → пустой список", () => {
    expect(lockChainRows([request({ blockingSessionId: null })])).toEqual([]);
  });
});

describe("waitCategory", () => {
  it("сопоставляет известные типы по префиксу (с суффиксами) доменной категории", () => {
    expect(waitCategory("PAGEIOLATCH_SH")).toBe("diskRead");
    expect(waitCategory("WRITELOG")).toBe("log");
    expect(waitCategory("LCK_M_IX")).toBe("lock");
    expect(waitCategory("PAGELATCH_EX")).toBe("pageContention");
    expect(waitCategory("SOS_SCHEDULER_YIELD")).toBe("cpu");
    expect(waitCategory("CXPACKET")).toBe("parallelism");
    expect(waitCategory("CXCONSUMER")).toBe("parallelism");
    expect(waitCategory("ASYNC_NETWORK_IO")).toBe("network");
    expect(waitCategory("RESOURCE_SEMAPHORE")).toBe("memoryGrant");
    expect(waitCategory("THREADPOOL")).toBe("threadpool");
    expect(waitCategory("BACKUPIO")).toBe("backup");
    expect(waitCategory("BACKUPBUFFER")).toBe("backup");
    expect(waitCategory("WAITFOR")).toBe("waitfor");
  });

  it("регистр входа не важен", () => {
    expect(waitCategory("pageiolatch_sh")).toBe("diskRead");
    expect(waitCategory("  lck_m_x  ")).toBe("lock");
  });

  it("нераспознанный тип → null", () => {
    expect(waitCategory("SOMETHING_ELSE")).toBeNull();
    expect(waitCategory("")).toBeNull();
  });
});

describe("formatInt", () => {
  it("null → «—», разрядные пробелы, знак минус типографский", () => {
    expect(formatInt(null)).toBe("—");
    expect(formatInt(0)).toBe("0");
    expect(formatInt(12_532)).toBe("12 532");
    expect(formatInt(-1_138_560)).toBe("−1 138 560");
  });
});
