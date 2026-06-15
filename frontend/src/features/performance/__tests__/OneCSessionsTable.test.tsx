import { render, screen, within } from "@testing-library/react";
import type { ReactNode } from "react";
import { describe, expect, it } from "vitest";
import "@/i18n";
import { TooltipProvider } from "@/components/ui/tooltip";
import { OneCSessionsTable } from "../OneCSessionsTable";
import type { OneCSessionLoad } from "../types";

const CAPTURED = "2026-06-08T12:00:00Z";

function renderTable(ui: ReactNode) {
  return render(<TooltipProvider>{ui}</TooltipProvider>);
}

function session(o: Partial<OneCSessionLoad>): OneCSessionLoad {
  return {
    sessionId: "11111111-1111-1111-1111-111111111111",
    sessionNumber: 1,
    clusterInfobaseId: "22222222-2222-2222-2222-222222222222",
    appId: "1CV8C",
    userName: "Иванов",
    host: "WS01",
    process: "33333333-3333-3333-3333-333333333333",
    connection: "44444444-4444-4444-4444-444444444444",
    cpuTimeCurrent: 100,
    durationCurrent: 200,
    durationCurrentDbms: 0,
    memoryCurrent: 0,
    blockedByDbms: 0,
    blockedByLs: 0,
    lastActiveAtUtc: CAPTURED,
    ...o,
  };
}

describe("OneCSessionsTable", () => {
  it("отсутствующие perf-поля рендерит как «—», не как 0", () => {
    renderTable(
      <OneCSessionsTable
        sessions={[session({ cpuTimeCurrent: null, durationCurrent: null, memoryCurrent: null })]}
        capturedAtUtc={CAPTURED}
      />
    );
    // три «—» в perf-колонках (ЦП, длит., память) + СУБД=0 рендерится как «0 мс»
    expect(screen.getAllByText("—").length).toBeGreaterThanOrEqual(3);
  });

  it("подсвечивает заблокированный сеанс", () => {
    renderTable(
      <OneCSessionsTable sessions={[session({ blockedByDbms: 3 })]} capturedAtUtc={CAPTURED} />
    );
    expect(screen.getByText("Заблокирован")).toBeInTheDocument();
  });

  it("долгий текущий вызов помечается как «Долгий вызов»", () => {
    renderTable(
      <OneCSessionsTable
        sessions={[session({ durationCurrent: 9_000 })]}
        capturedAtUtc={CAPTURED}
      />
    );
    expect(screen.getByText("Долгий вызов")).toBeInTheDocument();
  });

  it("пустой список — собственное пустое состояние таблицы", () => {
    renderTable(<OneCSessionsTable sessions={[]} capturedAtUtc={CAPTURED} />);
    expect(screen.getByText("Активных сеансов нет.")).toBeInTheDocument();
  });

  it("сортирует «кто грузит» по ЦП-времени вниз", () => {
    renderTable(
      <OneCSessionsTable
        sessions={[
          session({ sessionId: "low", sessionNumber: 7, cpuTimeCurrent: 10 }),
          session({ sessionId: "high", sessionNumber: 9, cpuTimeCurrent: 900 }),
        ]}
        capturedAtUtc={CAPTURED}
      />
    );
    const rows = screen.getAllByRole("row").slice(1); // без шапки
    expect(within(rows[0]).getByText("9")).toBeInTheDocument();
    expect(within(rows[1]).getByText("7")).toBeInTheDocument();
  });

  it("пустое имя пользователя — метка «без пользователя»", () => {
    renderTable(
      <OneCSessionsTable sessions={[session({ userName: "" })]} capturedAtUtc={CAPTURED} />
    );
    expect(screen.getByText("без пользователя")).toBeInTheDocument();
  });
});
