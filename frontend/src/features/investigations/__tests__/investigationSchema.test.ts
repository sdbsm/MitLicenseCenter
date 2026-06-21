import { describe, expect, it } from "vitest";
import {
  findingSchema,
  investigationDetailSchema,
  investigationSummarySchema,
  investigationsPagedSchema,
  progressSchema,
  reportSchema,
  type StartInvestigationRequest,
} from "../types";

/**
 * Parity-тесты контракта «Расследование» (MLC-239, инвариант parity BE↔FE). Главный риск (урок
 * [[api-omits-null-fields]], MLC-067/071): бэкенд сериализует `null`-поля ПРОПУСКОМ ключа
 * (`JsonIgnoreCondition.WhenWritingNull`). У активного/непривязанного дела `stoppedAtUtc`/`stopReason`/
 * `tenantId`/`infobaseId` не приходят как `null`, а ОТСУТСТВУЮТ — схема обязана это принимать
 * (`omittable()` = `.nullish()` + нормализация в `null`), иначе Zod-граница отвергнет ответ.
 */
describe("investigationSummarySchema (omit-null)", () => {
  it("принимает активное непривязанное дело с пропущенными nullable-полями → null", () => {
    const raw = {
      id: "11111111-1111-1111-1111-111111111111",
      scenario: "Locks",
      status: "Collecting",
      startedAtUtc: "2026-06-20T10:00:00Z",
      startedBy: "operator",
      findingsCount: 0,
      // stoppedAtUtc / stopReason / tenantId / infobaseId — ОТСУТСТВУЮТ (бэкенд опускает null)
    };
    const parsed = investigationSummarySchema.parse(raw);
    expect(parsed.stoppedAtUtc).toBeNull();
    expect(parsed.stopReason).toBeNull();
    expect(parsed.tenantId).toBeNull();
    expect(parsed.infobaseId).toBeNull();
    expect(parsed.status).toBe("Collecting");
  });

  it("принимает завершённое привязанное дело со всеми полями", () => {
    const parsed = investigationSummarySchema.parse({
      id: "22222222-2222-2222-2222-222222222222",
      scenario: "SlowQueries",
      status: "Completed",
      startedAtUtc: "2026-06-20T10:00:00Z",
      stoppedAtUtc: "2026-06-20T10:10:00Z",
      startedBy: "operator",
      stopReason: "Manual",
      tenantId: "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa",
      infobaseId: "bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb",
      findingsCount: 2,
    });
    expect(parsed.stopReason).toBe("Manual");
    expect(parsed.tenantId).toBe("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    expect(parsed.infobaseId).toBe("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
  });

  it("валидирует enum'ы статуса/сценария/причины по именам Domain-членов", () => {
    for (const status of [
      "Collecting",
      "Analyzing",
      "Completed",
      "Interrupted",
      "Failed",
    ] as const) {
      expect(
        investigationSummarySchema.parse({
          id: "33333333-3333-3333-3333-333333333333",
          scenario: "DbmsLocks",
          status,
          startedAtUtc: "2026-06-20T10:00:00Z",
          startedBy: "system",
          findingsCount: 0,
        }).status
      ).toBe(status);
    }
  });
});

describe("investigationsPagedSchema", () => {
  it("парсит конверт {items,total,page,pageSize}", () => {
    const parsed = investigationsPagedSchema.parse({
      items: [
        {
          id: "44444444-4444-4444-4444-444444444444",
          scenario: "Exceptions",
          status: "Failed",
          startedAtUtc: "2026-06-20T10:00:00Z",
          startedBy: "operator",
          findingsCount: 1,
        },
      ],
      total: 1,
      page: 1,
      pageSize: 100,
    });
    expect(parsed.items).toHaveLength(1);
    expect(parsed.items[0].status).toBe("Failed");
  });
});

describe("findingSchema", () => {
  // С MLC-243 result типизирован пер-Kind (Zod parity с BE-DTO анализаторов). Подробные
  // schema-тесты каждого Kind — в findingResultSchema.test.ts; здесь sanity на findingSchema-обёртке.
  it("принимает result как вложенный объект анализатора (SlowQueryAnalysisResult)", () => {
    const parsed = findingSchema.parse({
      kind: "SlowQueries",
      schemaVersion: 1,
      result: {
        topQueries: [{ durationMicroseconds: 6_000_000, durationSeconds: 6.0 }],
        similarGroups: [],
        totalDbmssqlEvents: 3,
        eventsAboveThreshold: 1,
        skippedEvents: 0,
      },
    });
    expect(parsed.kind).toBe("SlowQueries");
    expect((parsed.result as { totalDbmssqlEvents: number }).totalDbmssqlEvents).toBe(3);
  });
});

describe("investigationDetailSchema (omit-null config)", () => {
  it("принимает деталь с пропущенным collectionConfig (историческое дело) → null", () => {
    const parsed = investigationDetailSchema.parse({
      summary: {
        id: "55555555-5555-5555-5555-555555555555",
        scenario: "Locks",
        status: "Interrupted",
        startedAtUtc: "2026-06-20T10:00:00Z",
        startedBy: "system",
        findingsCount: 0,
      },
      // collectionConfig — ОТСУТСТВУЕТ (null опущен)
      findings: [],
    });
    expect(parsed.collectionConfig).toBeNull();
    expect(parsed.findings).toEqual([]);
  });

  it("принимает деталь со снимком сбора и находкой", () => {
    const parsed = investigationDetailSchema.parse({
      summary: {
        id: "66666666-6666-6666-6666-666666666666",
        scenario: "SlowQueries",
        status: "Completed",
        startedAtUtc: "2026-06-20T10:00:00Z",
        stoppedAtUtc: "2026-06-20T10:10:00Z",
        startedBy: "operator",
        stopReason: "TimeLimit",
        findingsCount: 1,
      },
      collectionConfig: {
        logcfgLocation: "C:\\techlog",
        events: "DBMSSQL,SDBL",
        // durationThresholdMicros присутствует, processNameFilter ОТСУТСТВУЕТ (весь кластер) → null
        durationThresholdMicros: 5000000,
        format: "json",
        historyHours: 2,
      },
      findings: [{ kind: "SlowQueries", schemaVersion: 1, result: {} }],
    });
    expect(parsed.collectionConfig?.processNameFilter).toBeNull();
    expect(parsed.collectionConfig?.durationThresholdMicros).toBe(5000000);
    expect(parsed.findings[0].kind).toBe("SlowQueries");
  });
});

describe("reportSchema", () => {
  it("парсит отчёт с ранжированными находками и серьёзностью", () => {
    const parsed = reportSchema.parse({
      summary: {
        id: "77777777-7777-7777-7777-777777777777",
        scenario: "Locks",
        status: "Completed",
        startedAtUtc: "2026-06-20T10:00:00Z",
        startedBy: "operator",
        findingsCount: 1,
      },
      generatedAtUtc: "2026-06-20T11:00:00Z",
      items: [
        {
          kind: "ManagedLocks",
          severity: "Warning",
          count: 12,
          headline: "Управляемые блокировки 1С",
          recommendation: "Разберите цепочки ожидания.",
        },
      ],
    });
    expect(parsed.items[0].severity).toBe("Warning");
    expect(parsed.items[0].count).toBe(12);
  });
});

describe("StartInvestigationRequest body (MLC-248 parity)", () => {
  // Контракт старта — TS-интерфейс (Zod-схемы запроса нет); проверяем форму тела на проводе.
  it("сериализует slowQueryThresholdSeconds как число (имя поля 1:1 с BE-DTO)", () => {
    const body: StartInvestigationRequest = {
      scenario: "SlowQueries",
      slowQueryThresholdSeconds: 2.5,
    };
    const json = JSON.parse(JSON.stringify(body));
    expect(json.scenario).toBe("SlowQueries");
    expect(json.slowQueryThresholdSeconds).toBe(2.5);
  });

  it("omit-null: без порога поле отсутствует в теле (бэкенд применит дефолт 1 c)", () => {
    const body: StartInvestigationRequest = { scenario: "Locks", infobaseId: "ib-1" };
    const json = JSON.parse(JSON.stringify(body));
    expect("slowQueryThresholdSeconds" in json).toBe(false);
    expect(json.infobaseId).toBe("ib-1");
  });

  it("явный 0 — валидное значение и сериализуется (все запросы в топ)", () => {
    const body: StartInvestigationRequest = {
      scenario: "GeneralSlow",
      slowQueryThresholdSeconds: 0,
    };
    const json = JSON.parse(JSON.stringify(body));
    expect(json.slowQueryThresholdSeconds).toBe(0);
  });
});

describe("progressSchema (omit-null collectedBytes)", () => {
  it("принимает прогресс завершённого дела с пропущенным collectedBytes → null", () => {
    const parsed = progressSchema.parse({
      id: "88888888-8888-8888-8888-888888888888",
      status: "Completed",
      startedAtUtc: "2026-06-20T10:00:00Z",
      elapsedSeconds: 120,
      // collectedBytes ОТСУТСТВУЕТ (каталог снят после завершения)
    });
    expect(parsed.collectedBytes).toBeNull();
    expect(parsed.elapsedSeconds).toBe(120);
  });

  it("принимает прогресс активного дела с размером собранного", () => {
    const parsed = progressSchema.parse({
      id: "99999999-9999-9999-9999-999999999999",
      status: "Collecting",
      startedAtUtc: "2026-06-20T10:00:00Z",
      elapsedSeconds: 30,
      collectedBytes: 4096,
    });
    expect(parsed.collectedBytes).toBe(4096);
    expect(parsed.status).toBe("Collecting");
  });
});
