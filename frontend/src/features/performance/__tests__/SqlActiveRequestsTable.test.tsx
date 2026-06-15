import { render, screen, within } from "@testing-library/react";
import { describe, expect, it } from "vitest";
import "@/i18n";
import { TooltipProvider } from "@/components/ui/tooltip";
import { SqlActiveRequestsTable } from "../SqlActiveRequestsTable";
import { buildAttributionMap } from "../sqlLoad";
import type { SqlActiveRequest, SqlDatabaseAttribution } from "../types";

const databases: SqlDatabaseAttribution[] = [
  {
    databaseName: "mitpro",
    tenantId: "t1",
    tenantName: "ООО Ромашка",
    infobaseName: "Бухгалтерия",
  },
];
const map = buildAttributionMap(databases);

function renderTable(requests: SqlActiveRequest[]) {
  return render(
    <TooltipProvider>
      <SqlActiveRequestsTable requests={requests} attributionMap={map} />
    </TooltipProvider>
  );
}

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
    cpuTimeMs: 100,
    elapsedMs: 200,
    logicalReads: 2048,
    sqlText: "SELECT 1",
    ...o,
  };
}

describe("SqlActiveRequestsTable", () => {
  it("отсутствующие perf-поля рендерит как «—», не как 0", () => {
    renderTable([
      request({
        sessionId: 5,
        cpuTimeMs: null,
        elapsedMs: null,
        logicalReads: null,
        sqlText: null,
      }),
    ]);
    expect(screen.getAllByText("—").length).toBeGreaterThanOrEqual(3);
    expect(screen.getByText("без текста")).toBeInTheDocument();
  });

  it("признак 1С — бейдж «1С»; клиент берётся из атрибуции по базе", () => {
    renderTable([request({ sessionId: 7, isOneC: true })]);
    expect(screen.getByText("1С")).toBeInTheDocument();
    expect(screen.getByText(/ООО Ромашка/)).toBeInTheDocument();
  });

  it("заблокированный сеанс — бейдж «ждёт сеанс N», блокирующий — «блокирует»", () => {
    renderTable([
      request({ sessionId: 10, blockingSessionId: 20 }),
      request({ sessionId: 20, blockingSessionId: null }),
    ]);
    expect(screen.getByText("ждёт сеанс 20")).toBeInTheDocument();
    expect(screen.getByText("блокирует")).toBeInTheDocument();
  });

  it("сортирует «кто грузит» по ЦП-времени вниз", () => {
    renderTable([
      request({ sessionId: 3, cpuTimeMs: 10 }),
      request({ sessionId: 9, cpuTimeMs: 900 }),
    ]);
    const rows = screen.getAllByRole("row").slice(1); // без шапки
    expect(within(rows[0]).getByText("9")).toBeInTheDocument();
    expect(within(rows[1]).getByText("3")).toBeInTheDocument();
  });

  it("пустой список — собственное пустое состояние таблицы", () => {
    renderTable([]);
    expect(screen.getByText("Активных запросов нет.")).toBeInTheDocument();
  });
});
