import { describe, expect, it } from "vitest";
import {
  findingSchema,
  lockAnalysisResultSchema,
  slowQueryAnalysisResultSchema,
  exceptionAnalysisResultSchema,
  dbmsLockAnalysisResultSchema,
} from "../types";

/**
 * Parity-тесты пер-Kind result схем (MLC-243, инвариант parity BE↔FE).
 *
 * Правила, которые проверяются для каждого Kind:
 * 1. Валидный полный payload (все обязательные поля) — парсится.
 * 2. Omit-null: nullable-поля ОТСУТСТВУЮТ в ответе (WhenWritingNull) → не валят схему,
 *    нормализуются в null.
 * 3. Лишний неизвестный ключ — не валит схему (Zod по умолчанию игнорирует; нет .strict()).
 * 4. Graceful в findingSchema: непарсящийся result → null, не бросает исключение.
 *
 * Данные обезличены (репозиторий публичный; реальных данных нет).
 */

// ─── ManagedLocks ────────────────────────────────────────────────────────────

describe("lockAnalysisResultSchema (kind=ManagedLocks)", () => {
  it("парсит полный payload с waitEdges, timeouts, deadlocks", () => {
    const parsed = lockAnalysisResultSchema.parse({
      waitEdges: [
        {
          ts: "20260621120000.000000",
          waitingSessionId: "1023",
          waitingUser: "UserA",
          waitingAppId: "1CV8C",
          blockingConnections: "1020",
          regions: "AccumulationRegister.Sales",
          lockMode: "Shared",
          waitDurationSeconds: 2.5,
          infobaseName: "demodb",
          rawProcessName: "demodb",
          context: "ОбщийМодуль.ОбработкаПолучения:30",
          database: "localhost\\demodb",
        },
      ],
      timeouts: [
        {
          ts: "20260621120001.000000",
          sessionId: "1023",
          user: "UserA",
          regions: "AccumulationRegister.Sales",
          lockMode: "Exclusive",
          waitDurationSeconds: 5.0,
          infobaseName: "demodb",
          rawProcessName: "demodb",
          context: "ОбщийМодуль.ОбработкаПолучения:35",
          waitConnections: "1020,1021",
        },
      ],
      deadlocks: [
        {
          ts: "20260621120002.000000",
          sessionId: "1020",
          user: "UserB",
          regions: "AccumulationRegister.Sales",
          lockMode: "Exclusive",
          durationSeconds: 0.1,
          infobaseName: "demodb",
          rawProcessName: "demodb",
          context: "ОбщийМодуль.Продажи:55",
          waitConnections: "1023",
        },
      ],
      tlockEventsProcessed: 42,
      skippedEvents: 0,
    });

    expect(parsed.waitEdges).toHaveLength(1);
    expect(parsed.waitEdges[0].waitDurationSeconds).toBe(2.5);
    expect(parsed.timeouts).toHaveLength(1);
    expect(parsed.deadlocks).toHaveLength(1);
    expect(parsed.tlockEventsProcessed).toBe(42);
    expect(parsed.skippedEvents).toBe(0);
  });

  it("omit-null: nullable-поля отсутствуют → нормализуются в null, не падают", () => {
    const parsed = lockAnalysisResultSchema.parse({
      waitEdges: [
        {
          // ts, waitingUser, blockingConnections, regions, lockMode, waitDurationSeconds,
          // infobaseName, rawProcessName, context, database — ОТСУТСТВУЮТ
          waitingSessionId: "2001",
          waitingAppId: "BackgroundJob",
        },
      ],
      timeouts: [],
      deadlocks: [],
      tlockEventsProcessed: 5,
      skippedEvents: 1,
    });

    const edge = parsed.waitEdges[0];
    expect(edge.ts).toBeNull();
    expect(edge.waitingUser).toBeNull();
    expect(edge.blockingConnections).toBeNull();
    expect(edge.regions).toBeNull();
    expect(edge.lockMode).toBeNull();
    expect(edge.waitDurationSeconds).toBeNull();
    expect(edge.infobaseName).toBeNull();
    expect(edge.rawProcessName).toBeNull();
    expect(edge.context).toBeNull();
    expect(edge.database).toBeNull();
  });

  it("лишний неизвестный ключ не валит схему", () => {
    expect(() =>
      lockAnalysisResultSchema.parse({
        waitEdges: [],
        timeouts: [],
        deadlocks: [],
        tlockEventsProcessed: 0,
        skippedEvents: 0,
        futureUnknownField: "someValue", // BE добавил новое поле
      })
    ).not.toThrow();
  });
});

// ─── SlowQueries ─────────────────────────────────────────────────────────────

describe("slowQueryAnalysisResultSchema (kind=SlowQueries)", () => {
  it("парсит полный payload с topQueries и similarGroups", () => {
    const parsed = slowQueryAnalysisResultSchema.parse({
      topQueries: [
        {
          ts: "20260621120000.000000",
          durationMicroseconds: 6500000,
          durationSeconds: 6.5,
          sql: "SELECT * FROM _AccumRgT12345 WHERE ...",
          context: "ОбщийМодуль.Продажи:100",
          dbPid: "55",
          rows: "1000",
          rowsAffected: null, // может прийти явным null из некоторых версий
          database: "localhost\\demodb",
          infobaseName: "demodb",
          rawProcessName: "demodb",
          sessionId: "2002",
          user: "UserA",
          planText: null,
        },
      ],
      similarGroups: [
        {
          normalizedSql: "SELECT * FROM _AccumRgT? WHERE ...",
          count: 5,
          totalDurationMicroseconds: 32500000,
          maxDurationMicroseconds: 7000000,
          totalDurationSeconds: 32.5,
          maxDurationSeconds: 7.0,
        },
      ],
      totalDbmssqlEvents: 100,
      eventsAboveThreshold: 5,
      skippedEvents: 0,
    });

    expect(parsed.topQueries).toHaveLength(1);
    expect(parsed.topQueries[0].durationSeconds).toBe(6.5);
    expect(parsed.topQueries[0].durationMicroseconds).toBe(6500000);
    expect(parsed.similarGroups).toHaveLength(1);
    expect(parsed.similarGroups[0].count).toBe(5);
    expect(parsed.totalDbmssqlEvents).toBe(100);
    expect(parsed.eventsAboveThreshold).toBe(5);
  });

  it("omit-null: nullable-поля отсутствуют → нормализуются в null, не падают", () => {
    const parsed = slowQueryAnalysisResultSchema.parse({
      topQueries: [
        {
          // Только обязательные числа; остальные — отсутствуют
          durationMicroseconds: 5000000,
          durationSeconds: 5.0,
          // ts, sql, context, dbPid, rows, rowsAffected, database, infobaseName,
          // rawProcessName, sessionId, user, planText — ОТСУТСТВУЮТ
        },
      ],
      similarGroups: [],
      totalDbmssqlEvents: 1,
      eventsAboveThreshold: 1,
      skippedEvents: 0,
    });

    const q = parsed.topQueries[0];
    expect(q.ts).toBeNull();
    expect(q.sql).toBeNull();
    expect(q.context).toBeNull();
    expect(q.dbPid).toBeNull();
    expect(q.rows).toBeNull();
    expect(q.rowsAffected).toBeNull();
    expect(q.database).toBeNull();
    expect(q.infobaseName).toBeNull();
    expect(q.rawProcessName).toBeNull();
    expect(q.sessionId).toBeNull();
    expect(q.user).toBeNull();
    expect(q.planText).toBeNull();
  });

  it("лишний неизвестный ключ в SlowQueryEntry не валит схему", () => {
    expect(() =>
      slowQueryAnalysisResultSchema.parse({
        topQueries: [
          {
            durationMicroseconds: 5000000,
            durationSeconds: 5.0,
            newFieldFromFutureVersion: 42,
          },
        ],
        similarGroups: [],
        totalDbmssqlEvents: 1,
        eventsAboveThreshold: 1,
        skippedEvents: 0,
      })
    ).not.toThrow();
  });
});

// ─── Exceptions ──────────────────────────────────────────────────────────────

describe("exceptionAnalysisResultSchema (kind=Exceptions)", () => {
  it("парсит полный payload с topExceptions", () => {
    const parsed = exceptionAnalysisResultSchema.parse({
      topExceptions: [
        {
          exceptionType: "DataBaseException",
          normalizedDescr: "Конфликт блокировок при выполнении транзакции",
          sampleDescr: "Конфликт блокировок при выполнении транзакции: deadlock",
          sampleContext: "ОбщийМодуль.Продажи:120",
          count: 4,
          isDatabaseException: true,
          infobaseName: "demodb",
          rawProcessName: "demodb",
          firstTs: "20260621120000.000000",
          lastTs: "20260621120100.000000",
        },
        {
          exceptionType: "MethodNotFoundException",
          normalizedDescr: "Метод объекта не обнаружен (#)",
          sampleDescr: "Метод объекта не обнаружен (ПолучитьДанные)",
          sampleContext: null,
          count: 2,
          isDatabaseException: false,
          infobaseName: "demodb",
          rawProcessName: "demodb",
          firstTs: "20260621120010.000000",
          lastTs: "20260621120050.000000",
        },
      ],
      totalExcpEvents: 6,
      databaseExceptionEvents: 4,
      skippedEvents: 0,
    });

    expect(parsed.topExceptions).toHaveLength(2);
    expect(parsed.topExceptions[0].isDatabaseException).toBe(true);
    expect(parsed.topExceptions[0].count).toBe(4);
    expect(parsed.topExceptions[1].isDatabaseException).toBe(false);
    expect(parsed.databaseExceptionEvents).toBe(4);
    expect(parsed.totalExcpEvents).toBe(6);
  });

  it("omit-null: nullable-поля отсутствуют → нормализуются в null, не падают", () => {
    const parsed = exceptionAnalysisResultSchema.parse({
      topExceptions: [
        {
          // exceptionType, sampleDescr, sampleContext, infobaseName, rawProcessName,
          // firstTs, lastTs — ОТСУТСТВУЮТ
          normalizedDescr: "(без описания)",
          count: 1,
          isDatabaseException: false,
        },
      ],
      totalExcpEvents: 1,
      databaseExceptionEvents: 0,
      skippedEvents: 0,
    });

    const g = parsed.topExceptions[0];
    expect(g.exceptionType).toBeNull();
    expect(g.sampleDescr).toBeNull();
    expect(g.sampleContext).toBeNull();
    expect(g.infobaseName).toBeNull();
    expect(g.rawProcessName).toBeNull();
    expect(g.firstTs).toBeNull();
    expect(g.lastTs).toBeNull();
    expect(g.normalizedDescr).toBe("(без описания)");
    expect(g.count).toBe(1);
    expect(g.isDatabaseException).toBe(false);
  });

  it("лишний неизвестный ключ не валит схему", () => {
    expect(() =>
      exceptionAnalysisResultSchema.parse({
        topExceptions: [],
        totalExcpEvents: 0,
        databaseExceptionEvents: 0,
        skippedEvents: 0,
        futureField: "x",
      })
    ).not.toThrow();
  });
});

// ─── DbmsLocks ───────────────────────────────────────────────────────────────

describe("dbmsLockAnalysisResultSchema (kind=DbmsLocks)", () => {
  it("парсит полный payload с waitEdges (sourceMatched=true)", () => {
    const parsed = dbmsLockAnalysisResultSchema.parse({
      waitEdges: [
        {
          victimTs: "20260621120000.000000",
          victimConnectId: "101",
          victimLksrc: "202",
          victimLkpto: "3",
          victimSql: "SELECT * FROM _AccumRgT12345",
          victimContext: "ОбщийМодуль.Продажи:50",
          victimLkpid: "55",
          sourceConnectId: "202",
          sourceLkato: "5",
          sourceLkaid: "56",
          sourceSql: "UPDATE _AccumRgT12345 SET ...",
          sourceContext: "ОбщийМодуль.Запасы:80",
          infobaseName: "demodb",
          rawProcessName: "demodb",
          database: "localhost\\demodb",
          sourceMatched: true,
        },
      ],
      lkEventsProcessed: 10,
      unmatchedVictimCount: 0,
      skippedEvents: 0,
    });

    expect(parsed.waitEdges).toHaveLength(1);
    expect(parsed.waitEdges[0].sourceMatched).toBe(true);
    expect(parsed.waitEdges[0].victimConnectId).toBe("101");
    expect(parsed.lkEventsProcessed).toBe(10);
    expect(parsed.unmatchedVictimCount).toBe(0);
  });

  it("парсит payload с частичным ребром (sourceMatched=false, источник не найден)", () => {
    const parsed = dbmsLockAnalysisResultSchema.parse({
      waitEdges: [
        {
          victimConnectId: "103",
          victimLksrc: "204",
          // sourceConnectId, sourceLkato, sourceLkaid, sourceSql, sourceContext — ОТСУТСТВУЮТ
          // victimTs, victimLkpto, victimSql, victimContext, victimLkpid — ОТСУТСТВУЮТ
          // infobaseName, rawProcessName, database — ОТСУТСТВУЮТ
          sourceMatched: false,
        },
      ],
      lkEventsProcessed: 5,
      unmatchedVictimCount: 1,
      skippedEvents: 0,
    });

    const edge = parsed.waitEdges[0];
    expect(edge.sourceMatched).toBe(false);
    expect(edge.victimTs).toBeNull();
    expect(edge.victimLkpto).toBeNull();
    expect(edge.victimSql).toBeNull();
    expect(edge.victimContext).toBeNull();
    expect(edge.victimLkpid).toBeNull();
    expect(edge.sourceConnectId).toBeNull();
    expect(edge.sourceLkato).toBeNull();
    expect(edge.sourceLkaid).toBeNull();
    expect(edge.sourceSql).toBeNull();
    expect(edge.sourceContext).toBeNull();
    expect(edge.infobaseName).toBeNull();
    expect(edge.rawProcessName).toBeNull();
    expect(edge.database).toBeNull();
  });

  it("лишний неизвестный ключ не валит схему", () => {
    expect(() =>
      dbmsLockAnalysisResultSchema.parse({
        waitEdges: [],
        lkEventsProcessed: 0,
        unmatchedVictimCount: 0,
        skippedEvents: 0,
        newBEField: true,
      })
    ).not.toThrow();
  });
});

// ─── findingSchema.result — Graceful degradation ─────────────────────────────

describe("findingSchema.result — graceful degradation", () => {
  it("ManagedLocks — result типизируется и парсится", () => {
    const parsed = findingSchema.parse({
      kind: "ManagedLocks",
      schemaVersion: 1,
      result: {
        waitEdges: [],
        timeouts: [],
        deadlocks: [],
        tlockEventsProcessed: 0,
        skippedEvents: 0,
      },
    });
    expect(parsed.kind).toBe("ManagedLocks");
    expect(parsed.result).not.toBeNull();
  });

  it("SlowQueries — result типизируется и парсится", () => {
    const parsed = findingSchema.parse({
      kind: "SlowQueries",
      schemaVersion: 1,
      result: {
        topQueries: [{ durationMicroseconds: 5000000, durationSeconds: 5.0 }],
        similarGroups: [],
        totalDbmssqlEvents: 1,
        eventsAboveThreshold: 1,
        skippedEvents: 0,
      },
    });
    expect(parsed.kind).toBe("SlowQueries");
    expect(parsed.result).not.toBeNull();
  });

  it("Exceptions — result типизируется и парсится", () => {
    const parsed = findingSchema.parse({
      kind: "Exceptions",
      schemaVersion: 1,
      result: {
        topExceptions: [
          { normalizedDescr: "(без описания)", count: 1, isDatabaseException: false },
        ],
        totalExcpEvents: 1,
        databaseExceptionEvents: 0,
        skippedEvents: 0,
      },
    });
    expect(parsed.kind).toBe("Exceptions");
    expect(parsed.result).not.toBeNull();
  });

  it("DbmsLocks — result типизируется и парсится", () => {
    const parsed = findingSchema.parse({
      kind: "DbmsLocks",
      schemaVersion: 1,
      result: {
        waitEdges: [{ sourceMatched: false }],
        lkEventsProcessed: 1,
        unmatchedVictimCount: 1,
        skippedEvents: 0,
      },
    });
    expect(parsed.kind).toBe("DbmsLocks");
    expect(parsed.result).not.toBeNull();
  });

  it("непарсящийся result (будущий schemaVersion с несовместимой формой) → null, не бросает", () => {
    // Намеренно передаём строку вместо объекта — полная несовместимость формы
    expect(() => {
      const parsed = findingSchema.parse({
        kind: "ManagedLocks",
        schemaVersion: 99,
        result: "incompatible_string_result",
      });
      // Должен вернуть null через .catch(null)
      expect(parsed.result).toBeNull();
    }).not.toThrow();
  });
});
