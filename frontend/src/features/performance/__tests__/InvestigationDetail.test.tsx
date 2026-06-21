import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { describe, expect, it, vi, beforeEach } from "vitest";
import "@/i18n";

/**
 * Тесты компонента InvestigationDetail — экран 3 (MLC-243/244, ADR-57).
 *
 * Проверяем:
 *   - Шапка (статус, сценарий, период, кнопка «Назад», кнопка «Отчёт» теперь активна)
 *   - Вердикт из useInvestigationReport
 *   - Блоки по kind найденных находок (ManagedLocks / SlowQueries / Exceptions / DbmsLocks)
 *   - Пустые состояния блоков (нет находок)
 *   - Кнопка «Назад» → onBack
 *   - Кнопка «Отчёт» → вызывает onOpenReport (MLC-244)
 */

// ── Фиктивные данные ─────────────────────────────────────────────────────────

const makeSummary = (overrides = {}) => ({
  id: "11111111-2222-3333-4444-555555555555",
  scenario: "Locks" as const,
  status: "Completed" as const,
  startedAtUtc: "2026-06-21T10:00:00Z",
  stoppedAtUtc: "2026-06-21T10:10:00Z",
  startedBy: "operator",
  stopReason: "Manual" as const,
  tenantId: null,
  infobaseId: null,
  findingsCount: 2,
  ...overrides,
});

const makeLockResult = () => ({
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
      context: "ОбщийМодуль.Продажи:30",
      database: null,
    },
  ],
  timeouts: [],
  deadlocks: [],
  tlockEventsProcessed: 5,
  skippedEvents: 0,
});

const makeSlowQueryResult = () => ({
  topQueries: [
    {
      ts: null,
      durationMicroseconds: 6500000,
      durationSeconds: 6.5,
      sql: "SELECT * FROM _AccumRgT12345",
      context: "ОбщийМодуль.Продажи:100",
      dbPid: null,
      rows: null,
      rowsAffected: null,
      database: null,
      infobaseName: "demodb",
      rawProcessName: "demodb",
      sessionId: null,
      user: null,
      planText: null,
    },
  ],
  similarGroups: [],
  totalDbmssqlEvents: 10,
  eventsAboveThreshold: 1,
  skippedEvents: 0,
});

const makeExceptionResult = () => ({
  topExceptions: [
    {
      exceptionType: "DataBaseException",
      normalizedDescr: "Конфликт блокировок при выполнении транзакции",
      sampleDescr: "Конфликт блокировок при выполнении транзакции: deadlock victim",
      sampleContext: null,
      count: 4,
      isDatabaseException: true,
      infobaseName: "demodb",
      rawProcessName: "demodb",
      firstTs: null,
      lastTs: null,
    },
  ],
  totalExcpEvents: 4,
  databaseExceptionEvents: 4,
  skippedEvents: 0,
});

const makeDbmsLockResult = () => ({
  waitEdges: [
    {
      victimTs: null,
      victimConnectId: "101",
      victimLksrc: "202",
      victimLkpto: null,
      victimSql: "SELECT * FROM _AccumRgT12345",
      victimContext: null,
      victimLkpid: null,
      sourceConnectId: "202",
      sourceLkato: null,
      sourceLkaid: null,
      sourceSql: "UPDATE _AccumRgT12345 SET ...",
      sourceContext: null,
      infobaseName: "demodb",
      rawProcessName: "demodb",
      database: null,
      sourceMatched: true,
    },
  ],
  lkEventsProcessed: 5,
  unmatchedVictimCount: 0,
  skippedEvents: 0,
});

const makeReport = (overrides = {}) => ({
  summary: makeSummary(),
  generatedAtUtc: "2026-06-21T11:00:00Z",
  items: [
    {
      kind: "ManagedLocks" as const,
      severity: "Warning" as const,
      count: 1,
      headline: "Обнаружены блокировки управляемого уровня",
      recommendation: "Разберите цепочки ожидания в 1С.",
    },
  ],
  ...overrides,
});

// ── Моки ─────────────────────────────────────────────────────────────────────

let mockDetailData: ReturnType<typeof makeDetail> | null = null;
let mockReportData: ReturnType<typeof makeReport> | null = null;

function makeDetail(kindResults: Array<{ kind: string; result: unknown }> = []) {
  return {
    summary: makeSummary(),
    collectionConfig: null,
    findings: kindResults.map((kr) => ({
      kind: kr.kind,
      schemaVersion: 1,
      result: kr.result,
    })),
  };
}

vi.mock("@/features/investigations/useInvestigations", () => ({
  useInvestigationDetail: () => ({
    data: mockDetailData,
    isLoading: false,
  }),
  useInvestigationReport: () => ({
    data: mockReportData,
    isLoading: false,
  }),
}));

vi.mock("@/features/infobases/useInfobases", () => ({
  useInfobases: () => ({
    data: {
      items: [{ id: "ib-1", name: "demodb", tenantName: "ООО «МитПро»" }],
    },
    isLoading: false,
  }),
}));

import { InvestigationDetail } from "../InvestigationDetail";

const mockOnBack = vi.fn();
const mockOnOpenReport = vi.fn();

function renderDetail() {
  return render(
    <InvestigationDetail
      investigationId="11111111-2222-3333-4444-555555555555"
      onBack={mockOnBack}
      onOpenReport={mockOnOpenReport}
    />
  );
}

// ── Тесты ────────────────────────────────────────────────────────────────────

describe("InvestigationDetail — карточка дела (MLC-243/244, экран 3)", () => {
  beforeEach(() => {
    mockDetailData = null;
    mockReportData = null;
    mockOnOpenReport.mockReset();
    vi.clearAllMocks();
  });

  it("нет данных → показывает «Нет данных»", () => {
    renderDetail();
    expect(screen.getByText("Нет данных")).toBeInTheDocument();
  });

  it("шапка: id (8 символов), статус-бейдж, сценарий, кнопки «Назад»/«Отчёт»", () => {
    mockDetailData = makeDetail();
    renderDetail();
    // Короткий id
    expect(screen.getByText("11111111")).toBeInTheDocument();
    // Статус
    expect(screen.getByText("Завершено")).toBeInTheDocument();
    // Сценарий
    expect(screen.getByText("Управляемые блокировки 1С")).toBeInTheDocument();
    // Кнопки
    expect(screen.getByRole("button", { name: /К списку/i })).toBeInTheDocument();
    // Кнопка «Отчёт» теперь активна (MLC-244)
    expect(screen.getByRole("button", { name: /Отчёт/i })).not.toBeDisabled();
  });

  it("кнопка «Назад» → вызывает onBack", async () => {
    const user = userEvent.setup();
    mockDetailData = makeDetail();
    renderDetail();
    await user.click(screen.getByRole("button", { name: /К списку/i }));
    expect(mockOnBack).toHaveBeenCalled();
  });

  it("кнопка «Отчёт» активна и вызывает onOpenReport (MLC-244)", async () => {
    const user = userEvent.setup();
    mockDetailData = makeDetail();
    renderDetail();
    const btn = screen.getByRole("button", { name: /Отчёт/i });
    expect(btn).not.toBeDisabled();
    await user.click(btn);
    expect(mockOnOpenReport).toHaveBeenCalledWith("11111111-2222-3333-4444-555555555555");
  });

  it("вердикт из useInvestigationReport отображается с headline и рекомендацией", () => {
    mockDetailData = makeDetail();
    mockReportData = makeReport();
    renderDetail();
    expect(screen.getByText("Обнаружены блокировки управляемого уровня")).toBeInTheDocument();
    expect(screen.getByText("Разберите цепочки ожидания в 1С.")).toBeInTheDocument();
    expect(screen.getByText("Внимание")).toBeInTheDocument();
  });

  it("пустой отчёт → показывает нейтральную плашку", () => {
    mockDetailData = makeDetail();
    mockReportData = { ...makeReport(), items: [] };
    renderDetail();
    expect(screen.getByText(/Вердикт недоступен/i)).toBeInTheDocument();
  });

  it("блок «Блокировки 1С» рендерится с waitEdge при kind=ManagedLocks", () => {
    mockDetailData = makeDetail([{ kind: "ManagedLocks", result: makeLockResult() }]);
    renderDetail();
    expect(screen.getByText("Цепочка блокировок 1С")).toBeInTheDocument();
    expect(screen.getByText("1023")).toBeInTheDocument(); // waitingSessionId
    expect(screen.getByText("AccumulationRegister.Sales")).toBeInTheDocument();
  });

  it("блок «Долгие запросы» рендерится при kind=SlowQueries", () => {
    mockDetailData = makeDetail([{ kind: "SlowQueries", result: makeSlowQueryResult() }]);
    renderDetail();
    expect(screen.getByText("Топ долгих запросов к СУБД")).toBeInTheDocument();
    // Длительность в сводке-метрике
    expect(screen.getByText("Долгих запросов")).toBeInTheDocument();
  });

  it("агрегат «похожие запросы» рендерится из similarGroups (MLC-248)", () => {
    const result = {
      ...makeSlowQueryResult(),
      similarGroups: [
        {
          normalizedSql: "SELECT T1._Field FROM _Hot T1 WHERE T1._Code = ?",
          count: 1000,
          totalDurationMicroseconds: 50_000_000,
          maxDurationMicroseconds: 50_000,
          totalDurationSeconds: 50,
          maxDurationSeconds: 0.05,
        },
      ],
    };
    mockDetailData = makeDetail([{ kind: "SlowQueries", result }]);
    renderDetail();
    expect(screen.getByText("Похожие запросы (по суммарному времени)")).toBeInTheDocument();
    expect(screen.getByText(/SELECT T1\._Field FROM _Hot/)).toBeInTheDocument();
    // count/total/max в статистике группы
    expect(screen.getByText(/1000 раз/)).toBeInTheDocument();
  });

  it("кейс «много мелких»: topQueries пуст, но similarGroups есть → агрегат показан (MLC-248)", () => {
    const result = {
      ...makeSlowQueryResult(),
      topQueries: [],
      eventsAboveThreshold: 0,
      similarGroups: [
        {
          normalizedSql: "SELECT ? FROM _Hot",
          count: 500,
          totalDurationMicroseconds: 25_000_000,
          maxDurationMicroseconds: 80_000,
          totalDurationSeconds: 25,
          maxDurationSeconds: 0.08,
        },
      ],
    };
    mockDetailData = makeDetail([{ kind: "SlowQueries", result }]);
    renderDetail();
    // Блок и агрегат присутствуют, несмотря на пустой топ.
    expect(screen.getByText("Топ долгих запросов к СУБД")).toBeInTheDocument();
    expect(screen.getByText("Похожие запросы (по суммарному времени)")).toBeInTheDocument();
    expect(screen.getByText(/500 раз/)).toBeInTheDocument();
  });

  it("блок «Исключения» рендерится с типом и описанием при kind=Exceptions", () => {
    mockDetailData = makeDetail([{ kind: "Exceptions", result: makeExceptionResult() }]);
    renderDetail();
    expect(screen.getByText("Исключения платформы 1С")).toBeInTheDocument();
    expect(screen.getByText("DataBaseException")).toBeInTheDocument();
    expect(screen.getByText("Ошибка СУБД")).toBeInTheDocument();
  });

  it("блок «СУБД-блокировки» рендерится отдельно при kind=DbmsLocks", () => {
    mockDetailData = makeDetail([{ kind: "DbmsLocks", result: makeDbmsLockResult() }]);
    renderDetail();
    expect(screen.getByText("СУБД-блокировки (уровень SQL Server)")).toBeInTheDocument();
    // victimConnectId и sourceConnectId
    expect(screen.getByText(/#101/)).toBeInTheDocument();
    expect(screen.getByText(/#202/)).toBeInTheDocument();
  });

  it("все четыре блока рендерятся при наличии всех Kind", () => {
    mockDetailData = makeDetail([
      { kind: "ManagedLocks", result: makeLockResult() },
      { kind: "SlowQueries", result: makeSlowQueryResult() },
      { kind: "Exceptions", result: makeExceptionResult() },
      { kind: "DbmsLocks", result: makeDbmsLockResult() },
    ]);
    renderDetail();
    expect(screen.getByText("Цепочка блокировок 1С")).toBeInTheDocument();
    expect(screen.getByText("Топ долгих запросов к СУБД")).toBeInTheDocument();
    expect(screen.getByText("Исключения платформы 1С")).toBeInTheDocument();
    expect(screen.getByText("СУБД-блокировки (уровень SQL Server)")).toBeInTheDocument();
  });

  it("нет находок → блоки не рендерятся", () => {
    mockDetailData = makeDetail([]); // нет findings
    renderDetail();
    expect(screen.queryByText("Цепочка блокировок 1С")).not.toBeInTheDocument();
    expect(screen.queryByText("Топ долгих запросов к СУБД")).not.toBeInTheDocument();
    expect(screen.queryByText("Исключения платформы 1С")).not.toBeInTheDocument();
    expect(screen.queryByText("СУБД-блокировки (уровень SQL Server)")).not.toBeInTheDocument();
  });

  it("scope resolveScope: infobaseId=ib-1 → «demodb»", () => {
    mockDetailData = {
      ...makeDetail(),
      summary: makeSummary({ infobaseId: "ib-1" }),
    };
    renderDetail();
    expect(screen.getByText("demodb")).toBeInTheDocument();
  });

  it("scope resolveScope: без infobaseId → «Весь узел»", () => {
    mockDetailData = makeDetail();
    renderDetail();
    expect(screen.getByText("Весь узел")).toBeInTheDocument();
  });
});
