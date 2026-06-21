import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { describe, expect, it, vi } from "vitest";
import "@/i18n";

/**
 * Тесты двухрежимной оболочки PerformancePage (MLC-241/242, ADR-57).
 *
 * Подход: мокаем ObservationMode и InvestigationMode, чтобы изолировать
 * логику переключателя от live-хуков (usePerformancePage / useDrillDownFocus /
 * React Query / useInvestigations). Тестируем только то, что живёт в контейнере:
 *   - дефолтный режим «Наблюдение»,
 *   - переключение на «Расследование» / обратно,
 *   - CTA-мост «Начать расследование» переключает режим.
 */

// Мок ObservationMode: рендерит маркер "observation-mode" и кнопку CTA-моста.
vi.mock("../ObservationMode", () => ({
  ObservationMode: ({ onStartInvestigation }: { onStartInvestigation: () => void }) => (
    <div data-testid="observation-mode">
      <button onClick={onStartInvestigation}>Начать расследование</button>
    </div>
  ),
}));

// Мок InvestigationMode: рендерит маркер "investigation-mode".
vi.mock("../InvestigationMode", () => ({
  InvestigationMode: () => <div data-testid="investigation-mode" />,
}));

import { PerformancePage } from "../PerformancePage";

function renderPage() {
  render(<PerformancePage />);
}

describe("PerformancePage — двухрежимная оболочка (MLC-241)", () => {
  it("по умолчанию отображается режим «Наблюдение»", () => {
    renderPage();
    expect(screen.getByTestId("observation-mode")).toBeInTheDocument();
    expect(screen.queryByTestId("investigation-mode")).not.toBeInTheDocument();
  });

  it("переключатель содержит вкладки «Наблюдение» и «Расследование»", () => {
    renderPage();
    expect(screen.getByRole("tab", { name: "Наблюдение" })).toBeInTheDocument();
    expect(screen.getByRole("tab", { name: "Расследование" })).toBeInTheDocument();
  });

  it("вкладка «Наблюдение» активна по умолчанию", () => {
    renderPage();
    expect(screen.getByRole("tab", { name: "Наблюдение" })).toHaveAttribute("data-state", "active");
    expect(screen.getByRole("tab", { name: "Расследование" })).toHaveAttribute(
      "data-state",
      "inactive"
    );
  });

  it("клик по вкладке «Расследование» показывает placeholder и скрывает observation", async () => {
    const user = userEvent.setup();
    renderPage();

    await user.click(screen.getByRole("tab", { name: "Расследование" }));

    expect(screen.getByTestId("investigation-mode")).toBeInTheDocument();
    expect(screen.queryByTestId("observation-mode")).not.toBeInTheDocument();
  });

  it("клик по вкладке «Наблюдение» возвращает observation-mode", async () => {
    const user = userEvent.setup();
    renderPage();

    await user.click(screen.getByRole("tab", { name: "Расследование" }));
    await user.click(screen.getByRole("tab", { name: "Наблюдение" }));

    expect(screen.getByTestId("observation-mode")).toBeInTheDocument();
    expect(screen.queryByTestId("investigation-mode")).not.toBeInTheDocument();
  });

  it("CTA-мост «Начать расследование» в ObservationMode переключает в режим расследования", async () => {
    const user = userEvent.setup();
    renderPage();

    // Кнопка «Начать расследование» рендерится внутри мока ObservationMode.
    await user.click(screen.getByRole("button", { name: "Начать расследование" }));

    expect(screen.getByTestId("investigation-mode")).toBeInTheDocument();
    expect(screen.queryByTestId("observation-mode")).not.toBeInTheDocument();
    // Вкладка «Расследование» должна стать активной.
    expect(screen.getByRole("tab", { name: "Расследование" })).toHaveAttribute(
      "data-state",
      "active"
    );
  });
});
