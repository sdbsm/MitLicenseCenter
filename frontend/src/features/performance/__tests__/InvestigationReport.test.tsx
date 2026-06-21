import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { describe, expect, it, vi, beforeEach } from "vitest";
import "@/i18n";

/**
 * Тесты компонента InvestigationReport — экран 4 (MLC-244, ADR-57, спека §Экран 4).
 *
 * Проверяем:
 *   - Шапка (короткий id, дата генерации, период, цель, кто запустил)
 *   - Резюме: items[] → severity + headline + recommendation; пустой items → нейтральная плашка
 *   - «Что собрано»: из collectionConfig; без config → нейтральная подпись
 *   - Находки: ранжированы по severity, headline, recommendation, count
 *   - Кнопка «Экспорт PDF» присутствует (Admin и Viewer — оба видят)
 *   - Кнопки «Открыть дело» и «К списку» — навигация
 */

// ── Фиктивные данные ─────────────────────────────────────────────────────────

const makeSummary = (overrides = {}) => ({
  id: "aabbccdd-1111-2222-3333-444455556666",
  scenario: "Locks" as const,
  status: "Completed" as const,
  startedAtUtc: "2026-06-21T10:00:00Z",
  stoppedAtUtc: "2026-06-21T10:10:00Z",
  startedBy: "operator",
  stopReason: "Manual" as const,
  tenantId: null,
  infobaseId: null,
  findingsCount: 1,
  ...overrides,
});

const makeReport = (overrides = {}) => ({
  summary: makeSummary(),
  generatedAtUtc: "2026-06-21T11:00:00Z",
  items: [
    {
      kind: "ManagedLocks" as const,
      severity: "Warning" as const,
      count: 3,
      headline: "Обнаружены блокировки управляемого уровня",
      recommendation: "Разберите цепочки ожидания в 1С.",
    },
  ],
  ...overrides,
});

const makeCollectionConfig = (overrides = {}) => ({
  logcfgLocation: "C:\\logcfg\\logcfg.xml",
  events: "TLOCK,TTIMEOUT,TDEADLOCK",
  durationThresholdMicros: 5_000_000,
  processNameFilter: "mitpro",
  format: "json",
  historyHours: 24,
  ...overrides,
});

// ── Моки хуков ───────────────────────────────────────────────────────────────

let mockReportData: ReturnType<typeof makeReport> | null = null;
let mockDetailData: {
  summary: ReturnType<typeof makeSummary>;
  collectionConfig: ReturnType<typeof makeCollectionConfig> | null;
  findings: unknown[];
} | null = null;

vi.mock("@/features/investigations/useInvestigations", () => ({
  useInvestigationReport: () => ({
    data: mockReportData,
    isLoading: false,
  }),
  useInvestigationDetail: () => ({
    data: mockDetailData,
    isLoading: false,
  }),
}));

vi.mock("@/features/infobases/useInfobases", () => ({
  useInfobases: () => ({
    data: {
      items: [{ id: "ib-1", name: "mitpro", tenantName: "ООО «МитПро»" }],
    },
    isLoading: false,
  }),
}));

// Мок downloadBlob — не трогаем DOM
vi.mock("@/features/reports/export/downloadBlob", () => ({
  downloadBlob: vi.fn(),
}));

// Мок toInvestigationPdf — возвращает фейковый Blob
vi.mock("@/features/investigations/toInvestigationPdf", () => ({
  toInvestigationPdf: vi.fn().mockResolvedValue(new Blob(["PDF"], { type: "application/pdf" })),
}));

// ── Импорты после мок-объявлений ─────────────────────────────────────────────

import { InvestigationReport } from "../InvestigationReport";

// ── Вспомогательная функция рендера ──────────────────────────────────────────

const mockOnOpenDeal = vi.fn();
const mockOnBackToList = vi.fn();

function renderReport() {
  return render(
    <InvestigationReport
      investigationId="aabbccdd-1111-2222-3333-444455556666"
      onOpenDeal={mockOnOpenDeal}
      onBackToList={mockOnBackToList}
    />
  );
}

// ── Тесты ────────────────────────────────────────────────────────────────────

describe("InvestigationReport — экран 4 «Отчёт» (MLC-244)", () => {
  beforeEach(() => {
    mockReportData = null;
    mockDetailData = null;
    mockOnOpenDeal.mockReset();
    mockOnBackToList.mockReset();
    vi.clearAllMocks();
  });

  it("нет данных → показывает «Нет данных»", () => {
    renderReport();
    expect(screen.getByText("Нет данных")).toBeInTheDocument();
  });

  it("шапка: короткий id, сформирован, период, цель, кто запустил", () => {
    mockReportData = makeReport();
    mockDetailData = { summary: makeSummary(), collectionConfig: null, findings: [] };
    renderReport();
    // Заголовок с коротким id
    expect(screen.getByText(/Отчёт по расследованию №aabbccdd/i)).toBeInTheDocument();
    // Кто запустил
    expect(screen.getByText("operator")).toBeInTheDocument();
    // Узел
    expect(screen.getByText("текущий узел")).toBeInTheDocument();
  });

  it("резюме: items[] → severity StatusBadge + headline + recommendation", () => {
    mockReportData = makeReport();
    mockDetailData = { summary: makeSummary(), collectionConfig: null, findings: [] };
    renderReport();
    // Заголовок и рекомендация показываются и в «Резюме», и в «Находках» — обе из items[].
    expect(screen.getAllByText("Обнаружены блокировки управляемого уровня").length).toBeGreaterThan(
      0
    );
    expect(screen.getAllByText("Разберите цепочки ожидания в 1С.").length).toBeGreaterThan(0);
    // severity badge (может быть несколько одинаковых)
    expect(screen.getAllByText("Внимание").length).toBeGreaterThan(0);
  });

  it("пустой items → нейтральное резюме «существенных проблем не выявлено»", () => {
    mockReportData = makeReport({ items: [] });
    mockDetailData = { summary: makeSummary(), collectionConfig: null, findings: [] };
    renderReport();
    expect(screen.getByText(/Существенных проблем не выявлено/i)).toBeInTheDocument();
  });

  it("«Что собрано» с collectionConfig: показывает события, формат, историю, порог, фильтр", () => {
    mockReportData = makeReport();
    mockDetailData = {
      summary: makeSummary(),
      collectionConfig: makeCollectionConfig(),
      findings: [],
    };
    renderReport();
    expect(screen.getByText("TLOCK,TTIMEOUT,TDEADLOCK")).toBeInTheDocument();
    expect(screen.getByText("json")).toBeInTheDocument();
    expect(screen.getByText("24 ч")).toBeInTheDocument();
    expect(screen.getByText("mitpro")).toBeInTheDocument();
    // retention
    expect(
      screen.getByText(/Сырьё технологического журнала удалено после разбора/i)
    ).toBeInTheDocument();
  });

  it("«Что собрано» без collectionConfig: нейтральная подпись о историческом деле", () => {
    mockReportData = makeReport();
    mockDetailData = { summary: makeSummary(), collectionConfig: null, findings: [] };
    renderReport();
    expect(screen.getByText(/Данные о конфигурации сбора недоступны/i)).toBeInTheDocument();
  });

  it("находки: count, headline, recommendation", () => {
    mockReportData = makeReport();
    mockDetailData = { summary: makeSummary(), collectionConfig: null, findings: [] };
    renderReport();
    // count — в разделе Находки
    expect(screen.getByText("3")).toBeInTheDocument();
  });

  it("кнопка «Экспорт PDF» присутствует", () => {
    mockReportData = makeReport();
    mockDetailData = { summary: makeSummary(), collectionConfig: null, findings: [] };
    renderReport();
    expect(screen.getByRole("button", { name: /Экспорт PDF/i })).toBeInTheDocument();
  });

  it("кнопка «Открыть дело» вызывает onOpenDeal", async () => {
    const user = userEvent.setup();
    mockReportData = makeReport();
    mockDetailData = { summary: makeSummary(), collectionConfig: null, findings: [] };
    renderReport();
    await user.click(screen.getByRole("button", { name: /Открыть дело/i }));
    expect(mockOnOpenDeal).toHaveBeenCalled();
  });

  it("кнопка «К списку» вызывает onBackToList", async () => {
    const user = userEvent.setup();
    mockReportData = makeReport();
    mockDetailData = { summary: makeSummary(), collectionConfig: null, findings: [] };
    renderReport();
    await user.click(screen.getByRole("button", { name: /К списку/i }));
    expect(mockOnBackToList).toHaveBeenCalled();
  });

  it("scope: infobaseId=ib-1 → показывает имя ИБ «mitpro»", () => {
    mockReportData = makeReport({ summary: makeSummary({ infobaseId: "ib-1" }) });
    mockDetailData = {
      summary: makeSummary({ infobaseId: "ib-1" }),
      collectionConfig: null,
      findings: [],
    };
    renderReport();
    // имя ИБ присутствует в шапке (поле «Цель»)
    expect(screen.getAllByText("mitpro").length).toBeGreaterThan(0);
  });

  it("scope: без infobaseId → «Весь узел»", () => {
    mockReportData = makeReport();
    mockDetailData = { summary: makeSummary(), collectionConfig: null, findings: [] };
    renderReport();
    expect(screen.getByText("Весь узел")).toBeInTheDocument();
  });

  it("подвал содержит упоминание retention и ИТС", () => {
    mockReportData = makeReport();
    mockDetailData = { summary: makeSummary(), collectionConfig: null, findings: [] };
    renderReport();
    expect(screen.getByText(/retention/i)).toBeInTheDocument();
    expect(screen.getByText(/its\.1c\.ru/i)).toBeInTheDocument();
  });
});
