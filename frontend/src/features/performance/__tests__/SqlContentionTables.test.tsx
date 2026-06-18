import { render, screen, within } from "@testing-library/react";
import { describe, expect, it } from "vitest";
import "@/i18n";
import { SqlDatabaseIoTable, SqlWaitsTable } from "../SqlContentionTables";
import { buildAttributionMap } from "../sqlLoad";
import type { SqlDatabaseAttribution, SqlDatabaseIo, SqlWaitDelta } from "../types";

const databases: SqlDatabaseAttribution[] = [
  {
    databaseName: "mitpro",
    tenantId: "t1",
    tenantName: "ООО Ромашка",
    infobaseName: "Бухгалтерия",
  },
];
const map = buildAttributionMap(databases);

function wait(o: Partial<SqlWaitDelta>): SqlWaitDelta {
  return { waitType: "PAGEIOLATCH_SH", waitTimeMsDelta: 100, waitingTasksDelta: 5, ...o };
}

function io(o: Partial<SqlDatabaseIo>): SqlDatabaseIo {
  return {
    databaseName: "mitpro",
    readStallMsDelta: 10,
    writeStallMsDelta: 20,
    readsDelta: 3,
    writesDelta: 4,
    ...o,
  };
}

describe("SqlWaitsTable — расшифровка смысла", () => {
  it("показывает расшифровку под распознанным типом ожидания", () => {
    render(<SqlWaitsTable waits={[wait({ waitType: "PAGEIOLATCH_SH" })]} />);
    expect(screen.getByText("PAGEIOLATCH_SH")).toBeInTheDocument();
    expect(screen.getByText(/Чтение страниц данных с диска/)).toBeInTheDocument();
  });

  it("не показывает расшифровку под нераспознанным типом", () => {
    render(<SqlWaitsTable waits={[wait({ waitType: "SOMETHING_ELSE" })]} />);
    expect(screen.getByText("SOMETHING_ELSE")).toBeInTheDocument();
    expect(screen.queryByText(/Чтение страниц данных/)).not.toBeInTheDocument();
  });
});

describe("SqlDatabaseIoTable — связка база→клиент", () => {
  it("показывает клиента (клиент · инфобаза) для атрибутированной базы", () => {
    render(<SqlDatabaseIoTable io={[io({ databaseName: "mitpro" })]} attributionMap={map} />);
    expect(screen.getByText("mitpro")).toBeInTheDocument();
    expect(screen.getByText("ООО Ромашка · Бухгалтерия")).toBeInTheDocument();
  });

  it("показывает «—» для неатрибутированной базы", () => {
    const { container } = render(
      <SqlDatabaseIoTable io={[io({ databaseName: "master" })]} attributionMap={map} />
    );
    const row = within(container).getByText("master").closest("tr")!;
    expect(within(row).getByText("—")).toBeInTheDocument();
  });
});
