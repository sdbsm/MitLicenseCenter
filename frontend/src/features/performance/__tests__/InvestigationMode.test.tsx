import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { describe, expect, it, vi, beforeEach } from "vitest";
import "@/i18n";

/**
 * Тесты режима «Расследование» (MLC-242/243, ADR-57):
 *   - Список дел (InvestigationList, экран 5, ДЕФОЛТ при отсутствии активного сбора)
 *   - Мастер запуска (InvestigationWizard, экран 2, открывается из списка)
 *   - Прогресс активного сбора (InvestigationProgress, экран 6)
 *   - Переключение InvestigationMode по состоянию активного дела
 *
 * Мокаем сетевые хуки (useInvestigations / useStartInvestigation /
 * useStopInvestigation / useInvestigationProgress / useInfobases / useMe /
 * useInvestigationDetail / useInvestigationReport / useDeleteInvestigation).
 */

// ── Фиктивные данные ─────────────────────────────────────────────────────────

const makeInvestigation = (
  status: "Collecting" | "Analyzing" | "Completed" | "Interrupted" | "Failed" = "Collecting",
  infobaseId?: string
) => ({
  id: "inv-001",
  scenario: "Locks" as const,
  status,
  startedAtUtc: "2026-06-21T10:00:00Z",
  startedBy: "admin",
  findingsCount: 0,
  // omittable-поля: нормализуются в null (omittable().transform возвращает T | null)
  stoppedAtUtc: null,
  stopReason: null,
  tenantId: null,
  infobaseId: infobaseId ?? null,
});

const makeProgress = (overrides = {}) => ({
  id: "inv-001",
  status: "Collecting" as const,
  startedAtUtc: "2026-06-21T10:00:00Z",
  elapsedSeconds: 90,
  collectedBytes: 2 * 1024 * 1024, // 2 МБ
  ...overrides,
});

const makeInfobase = (id: string, name: string, tenantName: string) => ({
  id,
  tenantId: "t1",
  tenantName,
  name,
  clusterInfobaseId: id,
  databaseName: name,
  status: "Active" as const,
  createdAt: "2026-01-01T00:00:00Z",
  publication: {
    id: "pub-" + id,
    infobaseId: id,
    siteName: "Default Web Site",
    virtualPath: "/" + name,
    platformVersion: "8.3.24",
    source: "Webinst" as const,
    createdAt: "2026-01-01T00:00:00Z",
    lastCheckStatus: "Published" as const,
  },
});

// ── Моки хуков ───────────────────────────────────────────────────────────────

const mockMutateAsyncStart = vi.fn();
const mockMutateAsyncStop = vi.fn();

// Дефолт: нет активных дел, Admin
let mockInvestigationsData: { items: ReturnType<typeof makeInvestigation>[] } = { items: [] };
let mockIsAdmin = true;
let mockProgressData: ReturnType<typeof makeProgress> | undefined = undefined;

const mockMutateAsyncDelete = vi.fn();

vi.mock("@/features/investigations/useInvestigations", () => ({
  useInvestigations: () => ({
    data: mockInvestigationsData,
    isLoading: false,
  }),
  useInvestigationProgress: () => ({
    data: mockProgressData,
  }),
  useInvestigationDetail: () => ({
    data: null,
    isLoading: false,
  }),
  useInvestigationReport: () => ({
    data: null,
    isLoading: false,
  }),
  useStartInvestigation: () => ({
    mutateAsync: mockMutateAsyncStart,
    isPending: false,
  }),
  useStopInvestigation: () => ({
    mutateAsync: mockMutateAsyncStop,
    isPending: false,
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
      items: [
        makeInfobase("ib-1", "ut11", "ООО «МитПро»"),
        makeInfobase("ib-2", "buh30", "ИП Петров"),
      ],
    },
    isLoading: false,
  }),
}));

// ── Импорты после мок-объявлений ─────────────────────────────────────────────

import { InvestigationMode } from "../InvestigationMode";
import { InvestigationWizard } from "../InvestigationWizard";
import { InvestigationProgress } from "../InvestigationProgress";

// ── Вспомогательная функция рендера ──────────────────────────────────────────

function renderMode() {
  return render(<InvestigationMode />);
}

// ── Тесты InvestigationMode (переключение мастер / прогресс) ─────────────────

describe("InvestigationMode — переключение по статусу активного дела (MLC-242/243)", () => {
  beforeEach(() => {
    mockInvestigationsData = { items: [] };
    mockIsAdmin = true;
    mockProgressData = undefined;
    vi.clearAllMocks();
  });

  it("без активного дела рендерится Список дел (дефолт MLC-243)", () => {
    renderMode();
    // Список дел содержит заголовок «Расследования» и кнопку «+ Новое расследование»
    expect(screen.getByText("Расследования")).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "+ Новое расследование" })).toBeInTheDocument();
  });

  it("без активного дела НЕ показывает Мастер сразу", () => {
    renderMode();
    // Мастер «Новое расследование» — это заголовок карточки мастера, не кнопки
    expect(screen.queryByText("Выберите сценарий и настройте цель сбора")).not.toBeInTheDocument();
  });

  it("при активном деле (Collecting) рендерится Прогресс, а не Список", () => {
    mockInvestigationsData = { items: [makeInvestigation("Collecting")] };
    mockProgressData = makeProgress();
    renderMode();
    expect(screen.getByText("Идёт сбор")).toBeInTheDocument();
    expect(screen.queryByText("Расследования")).not.toBeInTheDocument();
  });

  it("при активном деле (Analyzing) рендерится Прогресс со статусом «Анализ»", () => {
    mockInvestigationsData = { items: [makeInvestigation("Analyzing")] };
    mockProgressData = makeProgress({ status: "Analyzing" });
    renderMode();
    expect(screen.getByText("Идёт анализ")).toBeInTheDocument();
    expect(screen.queryByText("Расследования")).not.toBeInTheDocument();
  });

  it("завершённое дело (Completed) не считается активным → показывает Список дел", () => {
    mockInvestigationsData = { items: [makeInvestigation("Completed")] };
    renderMode();
    expect(screen.getByText("Расследования")).toBeInTheDocument();
  });

  it("клик «+ Новое расследование» из списка → открывает Мастер", async () => {
    const user = userEvent.setup();
    renderMode();
    await user.click(screen.getByRole("button", { name: "+ Новое расследование" }));
    expect(screen.getByText("Новое расследование")).toBeInTheDocument();
  });
});

// ── Тесты InvestigationWizard ─────────────────────────────────────────────────

describe("InvestigationWizard — Мастер запуска (MLC-242, экран 2)", () => {
  beforeEach(() => {
    mockInvestigationsData = { items: [] };
    mockIsAdmin = true;
    vi.clearAllMocks();
  });

  it("рендерит выбор сценария (4 варианта в списке, без DbmsLocks)", async () => {
    const user = userEvent.setup();
    render(<InvestigationWizard />);
    // Открываем Select-триггер
    await user.click(screen.getByRole("combobox"));
    expect(screen.getByRole("option", { name: "Управляемые блокировки 1С" })).toBeInTheDocument();
    expect(screen.getByRole("option", { name: "Долгие запросы к СУБД" })).toBeInTheDocument();
    expect(screen.getByRole("option", { name: "Исключения платформы" })).toBeInTheDocument();
    expect(screen.getByRole("option", { name: "Общая медленная работа" })).toBeInTheDocument();
    // DbmsLocks не показываем в мастере
    expect(
      screen.queryByRole("option", { name: "Блокировки уровня СУБД" })
    ).not.toBeInTheDocument();
  });

  it("рендерит выбор scope: «Весь узел» и «Конкретная инфобаза» (нативные radio)", () => {
    render(<InvestigationWizard />);
    // Нативные <input type="radio"> внутри <label> с текстом
    expect(screen.getByText("Весь узел (все инфобазы)")).toBeInTheDocument();
    expect(screen.getByText("Конкретная инфобаза")).toBeInTheDocument();
    // Обе radio кнопки присутствуют
    const radios = screen.getAllByRole("radio");
    expect(radios).toHaveLength(2);
    // По умолчанию «Весь узел» выбран
    expect(radios[0]).toBeChecked();
  });

  it("кнопка «Запустить» задизейблена, пока не выбран сценарий", () => {
    render(<InvestigationWizard />);
    expect(screen.getByRole("button", { name: "Запустить" })).toBeDisabled();
  });

  it("выбор сценария + Весь узел → кнопка становится активной, клик вызывает useStartInvestigation с {scenario}", async () => {
    mockMutateAsyncStart.mockResolvedValue({});
    const user = userEvent.setup();
    render(<InvestigationWizard />);

    // Открываем Select и выбираем сценарий
    await user.click(screen.getByRole("combobox"));
    await user.click(screen.getByRole("option", { name: "Управляемые блокировки 1С" }));

    const startBtn = screen.getByRole("button", { name: "Запустить" });
    expect(startBtn).not.toBeDisabled();

    await user.click(startBtn);
    expect(mockMutateAsyncStart).toHaveBeenCalledWith({ scenario: "Locks" });
  });

  it("выбор «Конкретная инфобаза» показывает дропдаун ИБ", async () => {
    const user = userEvent.setup();
    render(<InvestigationWizard />);

    // Клик на radio «Конкретная инфобаза»
    const radios = screen.getAllByRole("radio");
    await user.click(radios[1]); // второй radio = "infobase"
    // После выбора должен появиться SearchableSelect (role=combobox, aria-label=Инфобаза)
    expect(screen.getByRole("combobox", { name: "Инфобаза" })).toBeInTheDocument();
  });

  it("при выборе ИБ + сценарий — Запустить вызывает useStartInvestigation с {scenario, infobaseId}", async () => {
    mockMutateAsyncStart.mockResolvedValue({});
    const user = userEvent.setup();
    render(<InvestigationWizard />);

    // Выбираем сценарий через Select (role=combobox без aria-label — это Radix Select)
    // SearchableSelect тоже role=combobox но появляется позже; сначала есть только один combobox
    await user.click(screen.getByRole("combobox"));
    await user.click(screen.getByRole("option", { name: "Долгие запросы к СУБД" }));

    // Переключаем scope на «Конкретная инфобаза»
    const radios = screen.getAllByRole("radio");
    await user.click(radios[1]);

    // Открываем дропдаун ИБ — ищем по aria-label «Инфобаза»
    await user.click(screen.getByRole("combobox", { name: "Инфобаза" }));
    const option = await screen.findByText("ut11 — ООО «МитПро»");
    await user.click(option);

    // Теперь кнопка «Запустить» должна быть доступна
    const startBtn = screen.getByRole("button", { name: "Запустить" });
    expect(startBtn).not.toBeDisabled();
    await user.click(startBtn);

    expect(mockMutateAsyncStart).toHaveBeenCalledWith({
      scenario: "SlowQueries",
      infobaseId: "ib-1",
    });
  });

  it("роль-гейт: Viewer видит disabled-кнопку «Запустить»", async () => {
    mockIsAdmin = false;
    const user = userEvent.setup();
    render(<InvestigationWizard />);

    // Выбираем сценарий через Select
    await user.click(screen.getByRole("combobox"));
    await user.click(screen.getByRole("option", { name: "Управляемые блокировки 1С" }));

    // Кнопка всё равно задизейблена для Viewer
    expect(screen.getByRole("button", { name: "Запустить" })).toBeDisabled();
    // mutateAsync не вызывался
    expect(mockMutateAsyncStart).not.toHaveBeenCalled();
  });
});

// ── Тесты InvestigationProgress ───────────────────────────────────────────────

describe("InvestigationProgress — прогресс сбора (MLC-242, экран 6)", () => {
  beforeEach(() => {
    mockIsAdmin = true;
    vi.clearAllMocks();
  });

  it("в статусе Collecting показывает заголовок «Идёт сбор», сценарий, время, объём", () => {
    const summary = makeInvestigation("Collecting");
    const progress = makeProgress();
    render(<InvestigationProgress summary={summary} progress={progress} />);

    expect(screen.getByText("Идёт сбор")).toBeInTheDocument();
    expect(screen.getByText("Управляемые блокировки 1С")).toBeInTheDocument();
    // 90 сек = 1 мин 30 сек
    expect(screen.getByText("1 мин 30 сек")).toBeInTheDocument();
    // 2 МБ
    expect(screen.getByText("2.0 МБ")).toBeInTheDocument();
  });

  it("Admin видит кнопку «Остановить сейчас» в статусе Collecting", () => {
    const summary = makeInvestigation("Collecting");
    render(<InvestigationProgress summary={summary} progress={makeProgress()} />);
    expect(screen.getByRole("button", { name: /Остановить сейчас/i })).toBeInTheDocument();
  });

  it("клик «Остановить сейчас» вызывает useStopInvestigation(activeId)", async () => {
    mockMutateAsyncStop.mockResolvedValue({});
    const user = userEvent.setup();
    const summary = makeInvestigation("Collecting");
    render(<InvestigationProgress summary={summary} progress={makeProgress()} />);

    await user.click(screen.getByRole("button", { name: /Остановить сейчас/i }));
    expect(mockMutateAsyncStop).toHaveBeenCalledWith("inv-001");
  });

  it("в статусе Analyzing показывает «Идёт анализ» без кнопки стоп", () => {
    const summary = makeInvestigation("Analyzing");
    const progress = makeProgress({ status: "Analyzing" });
    render(<InvestigationProgress summary={summary} progress={progress} />);

    expect(screen.getByText("Идёт анализ")).toBeInTheDocument();
    expect(screen.queryByRole("button", { name: /Остановить/i })).not.toBeInTheDocument();
  });

  it("Viewer не видит кнопку «Остановить сейчас»", () => {
    mockIsAdmin = false;
    const summary = makeInvestigation("Collecting");
    render(<InvestigationProgress summary={summary} progress={makeProgress()} />);
    expect(screen.queryByRole("button", { name: /Остановить сейчас/i })).not.toBeInTheDocument();
  });

  it("без collectedBytes — строка «Собрано» не рендерится", () => {
    const summary = makeInvestigation("Collecting");
    const progress = makeProgress({ collectedBytes: undefined });
    render(<InvestigationProgress summary={summary} progress={progress} />);
    expect(screen.queryByText(/МБ|КБ/)).not.toBeInTheDocument();
  });

  it("показывает гарантию авто-снятия logcfg", () => {
    const summary = makeInvestigation("Collecting");
    render(<InvestigationProgress summary={summary} progress={makeProgress()} />);
    expect(screen.getByText(/logcfg снимется автоматически/i)).toBeInTheDocument();
  });
});
