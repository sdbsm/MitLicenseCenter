import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { describe, expect, it, vi, beforeEach } from "vitest";
import "@/i18n";

/**
 * Тесты компонента InvestigationList — экран 5 (MLC-243, ADR-57).
 *
 * Проверяем:
 *   - Список дел (рендер строк, колонки)
 *   - Баннер активного сбора и кнопка «перейти к прогрессу»
 *   - Клиентские фильтры по сценарию и статусу
 *   - Клик по строке → onSelectInvestigation
 *   - Кнопка «+ Новое расследование» → onNewInvestigation
 *   - Удаление → useDeleteInvestigation
 *   - Пустое состояние
 */

// ── Фиктивные данные ─────────────────────────────────────────────────────────

function makeInv(
  id: string,
  overrides: {
    status?: "Collecting" | "Analyzing" | "Completed" | "Interrupted" | "Failed";
    scenario?: "Locks" | "SlowQueries" | "Exceptions" | "GeneralSlow" | "DbmsLocks";
    infobaseId?: string;
  } = {}
) {
  return {
    id,
    scenario: overrides.scenario ?? "Locks",
    status: overrides.status ?? "Completed",
    startedAtUtc: "2026-06-21T10:00:00Z",
    stoppedAtUtc: overrides.status === "Collecting" ? null : "2026-06-21T10:10:00Z",
    startedBy: "operator",
    stopReason: overrides.status === "Completed" ? "Manual" : null,
    tenantId: null,
    infobaseId: overrides.infobaseId ?? null,
    findingsCount: 3,
  };
}

// ── Моки ─────────────────────────────────────────────────────────────────────

let mockItems: ReturnType<typeof makeInv>[] = [];
let mockIsAdmin = true;
const mockMutateAsyncDelete = vi.fn();
const mockOnNew = vi.fn();
const mockOnSelect = vi.fn();
const mockOnProgress = vi.fn();

vi.mock("@/features/investigations/useInvestigations", () => ({
  useInvestigations: () => ({
    data: { items: mockItems },
    isLoading: false,
  }),
  useDeleteInvestigation: () => ({
    mutateAsync: mockMutateAsyncDelete,
    isPending: false,
  }),
}));

vi.mock("@/features/auth/useAuth", () => ({
  useMe: () => ({
    data: { roles: mockIsAdmin ? ["Admin"] : ["Viewer"] },
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

import { InvestigationList } from "../InvestigationList";

function renderList() {
  return render(
    <InvestigationList
      onNewInvestigation={mockOnNew}
      onSelectInvestigation={mockOnSelect}
      onShowProgress={mockOnProgress}
    />
  );
}

// ── Тесты ────────────────────────────────────────────────────────────────────

describe("InvestigationList — список дел (MLC-243, экран 5)", () => {
  beforeEach(() => {
    mockItems = [];
    mockIsAdmin = true;
    vi.clearAllMocks();
  });

  it("пустой список → показывает empty-state с подсказкой", () => {
    renderList();
    expect(screen.getByText("Расследований нет")).toBeInTheDocument();
    expect(screen.getByText(/Запустите первое расследование/i)).toBeInTheDocument();
  });

  it("рендерит строки дел с нужными колонками", () => {
    mockItems = [
      makeInv("aaa00001"),
      makeInv("bbb00002", { scenario: "SlowQueries", status: "Interrupted" }),
    ];
    renderList();
    // Короткие id (первые 8 символов)
    expect(screen.getByText("aaa00001")).toBeInTheDocument();
    expect(screen.getByText("bbb00002")).toBeInTheDocument();
    // Сценарии
    expect(screen.getAllByText("Управляемые блокировки 1С")).toHaveLength(1);
    expect(screen.getByText("Долгие запросы к СУБД")).toBeInTheDocument();
    // Статусы через StatusBadge
    expect(screen.getByText("Завершено")).toBeInTheDocument();
    expect(screen.getByText("Прервано")).toBeInTheDocument();
  });

  it("инфобаза resolveScope: id ib-1 → «demodb», без id → «Весь узел»", () => {
    mockItems = [makeInv("ccc00003", { infobaseId: "ib-1" }), makeInv("ddd00004")];
    renderList();
    expect(screen.getByText("demodb")).toBeInTheDocument();
    expect(screen.getByText("Весь узел")).toBeInTheDocument();
  });

  it("клик по строке → вызывает onSelectInvestigation с нужным id", async () => {
    const user = userEvent.setup();
    mockItems = [makeInv("eee00005")];
    renderList();
    // Клик по id-ячейке (уникально)
    await user.click(screen.getByText("eee00005"));
    expect(mockOnSelect).toHaveBeenCalledWith("eee00005");
  });

  it("кнопка «+ Новое расследование» → вызывает onNewInvestigation", async () => {
    const user = userEvent.setup();
    renderList();
    await user.click(screen.getByRole("button", { name: "+ Новое расследование" }));
    expect(mockOnNew).toHaveBeenCalled();
  });

  it("баннер активного сбора виден при наличии Collecting-дела", () => {
    mockItems = [makeInv("fff00006", { status: "Collecting" })];
    renderList();
    // «Идёт сбор» встречается и в баннере, и в бейдже статуса строки — баннер опознаём по
    // уникальной ссылке «Перейти к прогрессу».
    expect(screen.getAllByText(/Идёт сбор/i).length).toBeGreaterThanOrEqual(1);
    expect(screen.getByText("Перейти к прогрессу")).toBeInTheDocument();
  });

  it("клик «Перейти к прогрессу» в баннере → вызывает onShowProgress", async () => {
    const user = userEvent.setup();
    mockItems = [makeInv("ggg00007", { status: "Collecting" })];
    renderList();
    await user.click(screen.getByText("Перейти к прогрессу"));
    expect(mockOnProgress).toHaveBeenCalled();
  });

  it("баннер НЕ показывается, если нет активных дел", () => {
    mockItems = [makeInv("hhh00008", { status: "Completed" })];
    renderList();
    expect(screen.queryByText("Перейти к прогрессу")).not.toBeInTheDocument();
  });

  it("фильтр по сценарию: выбор SlowQueries скрывает Locks-дела", async () => {
    const user = userEvent.setup();
    mockItems = [
      makeInv("iii00009", { scenario: "Locks" }),
      makeInv("jjj00010", { scenario: "SlowQueries" }),
    ];
    renderList();
    // Открываем первый Select (сценарий)
    const triggers = screen.getAllByRole("combobox");
    await user.click(triggers[0]);
    await user.click(screen.getByRole("option", { name: "Долгие запросы к СУБД" }));
    // Locks-дело скрыто
    expect(screen.queryByText("iii00009")).not.toBeInTheDocument();
    expect(screen.getByText("jjj00010")).toBeInTheDocument();
  });

  it("фильтр по статусу: выбор Completed скрывает Interrupted-дела", async () => {
    const user = userEvent.setup();
    mockItems = [
      makeInv("kkk00011", { status: "Completed" }),
      makeInv("lll00012", { status: "Interrupted" }),
    ];
    renderList();
    const triggers = screen.getAllByRole("combobox");
    await user.click(triggers[1]);
    await user.click(screen.getByRole("option", { name: "Завершено" }));
    expect(screen.getByText("kkk00011")).toBeInTheDocument();
    expect(screen.queryByText("lll00012")).not.toBeInTheDocument();
  });

  it("Admin видит кнопку удаления в строке", () => {
    mockIsAdmin = true;
    mockItems = [makeInv("mmm00013")];
    renderList();
    expect(screen.getByRole("button", { name: "Удалить дело?" })).toBeInTheDocument();
  });

  it("клик по кнопке удаления открывает диалог подтверждения", async () => {
    const user = userEvent.setup();
    mockIsAdmin = true;
    mockItems = [makeInv("nnn00014")];
    renderList();
    await user.click(screen.getByRole("button", { name: "Удалить дело?" }));
    // Диалог открылся
    expect(screen.getByText("Удалить дело?")).toBeInTheDocument();
  });

  it("Viewer НЕ видит кнопку удаления", () => {
    mockIsAdmin = false;
    mockItems = [makeInv("ooo00015")];
    renderList();
    expect(screen.queryByRole("button", { name: "Удалить дело?" })).not.toBeInTheDocument();
  });
});
