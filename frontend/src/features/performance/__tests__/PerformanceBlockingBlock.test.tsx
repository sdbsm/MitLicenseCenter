import { render, screen } from "@testing-library/react";
import { describe, expect, it, vi } from "vitest";
import "@/i18n";
import { TooltipProvider } from "@/components/ui/tooltip";
import type { OneCLoadSnapshot, SqlActiveRequest, SqlPerformanceView } from "../types";

// Live-хуки замоканы — в компонент-тест React Query / сеть не тянем (паттерн perf-тестов).
const useOneCLoad = vi.fn();
const useSqlPerformance = vi.fn();
vi.mock("../useOneCLoad", () => ({ useOneCLoad: () => useOneCLoad() }));
vi.mock("../useSqlPerformance", () => ({ useSqlPerformance: () => useSqlPerformance() }));

import { PerformanceBlockingBlock } from "../PerformanceBlockingBlock";

function oneCSnapshot(sessions: OneCLoadSnapshot["sessions"]): OneCLoadSnapshot {
  return { capturedAtUtc: "2026-06-18T12:00:00Z", sessions, processes: [] };
}

function sqlView(requests: SqlActiveRequest[]): SqlPerformanceView {
  return {
    snapshot: {
      capturedAtUtc: "2026-06-18T12:00:00Z",
      status: "Ok",
      measuring: false,
      activeRequests: requests,
      databaseIo: [],
      topWaits: [],
    },
    databases: [],
  };
}

function oneCSession(
  o: Partial<OneCLoadSnapshot["sessions"][number]>
): OneCLoadSnapshot["sessions"][number] {
  return {
    sessionId: "s1",
    sessionNumber: 1,
    clusterInfobaseId: "ib1",
    appId: "1CV8C",
    userName: "Иванов",
    host: "WS01",
    process: null,
    connection: null,
    cpuTimeCurrent: 0,
    durationCurrent: 0,
    durationCurrentDbms: 0,
    memoryCurrent: 0,
    blockedByDbms: 0,
    blockedByLs: 0,
    lastActiveAtUtc: null,
    ...o,
  };
}

function sqlRequest(o: Partial<SqlActiveRequest>): SqlActiveRequest {
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
    sqlText: null,
    ...o,
  };
}

function renderBlock() {
  return render(
    <TooltipProvider>
      <PerformanceBlockingBlock paused={false} />
    </TooltipProvider>
  );
}

describe("PerformanceBlockingBlock", () => {
  it("обе пустые → нейтральное «Блокировок нет»", () => {
    useOneCLoad.mockReturnValue({ data: oneCSnapshot([]) });
    useSqlPerformance.mockReturnValue({ data: sqlView([]) });
    renderBlock();
    expect(screen.getByText("Блокировок нет")).toBeInTheDocument();
  });

  it("1С-заблокированный сеанс → его № и «СУБД: сеанс N»", () => {
    useOneCLoad.mockReturnValue({
      data: oneCSnapshot([oneCSession({ sessionNumber: 42, blockedByDbms: 17 })]),
    });
    useSqlPerformance.mockReturnValue({ data: sqlView([]) });
    renderBlock();
    expect(screen.getByText("1С — заблокированные сеансы")).toBeInTheDocument();
    expect(screen.getByText("42")).toBeInTheDocument();
    expect(screen.getByText("СУБД: сеанс 17")).toBeInTheDocument();
  });

  it("SQL-заблокированный запрос → бейдж «ждёт сеанс N» и имя базы", () => {
    useOneCLoad.mockReturnValue({ data: oneCSnapshot([]) });
    useSqlPerformance.mockReturnValue({
      data: sqlView([sqlRequest({ sessionId: 5, blockingSessionId: 88, databaseName: "mitpro" })]),
    });
    renderBlock();
    expect(screen.getByText("SQL — цепочки блокировок")).toBeInTheDocument();
    expect(screen.getByText("ждёт сеанс 88")).toBeInTheDocument();
    expect(screen.getByText("mitpro")).toBeInTheDocument();
  });
});
