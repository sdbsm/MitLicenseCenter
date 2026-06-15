import { describe, it, expect, vi } from "vitest";
import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import {
  getCoreRowModel,
  getPaginationRowModel,
  getSortedRowModel,
  useReactTable,
  type SortingState,
} from "@tanstack/react-table";
import { useState } from "react";
import "@/i18n";
import i18n from "@/i18n";
import { TooltipProvider } from "@/components/ui/tooltip";
import { SessionsTable } from "../SessionsTable";
import { buildSessionColumns } from "../sessionColumns";
import type { SessionSnapshotEntry } from "../types";

function row(overrides: Partial<SessionSnapshotEntry>): SessionSnapshotEntry {
  return {
    sessionId: "11111111-1111-1111-1111-111111111111",
    clusterInfobaseId: "22222222-2222-2222-2222-222222222222",
    tenantId: "33333333-3333-3333-3333-333333333333",
    tenantName: "Acme",
    infobaseName: "БП",
    appId: "1CV8C",
    userName: "Андрей",
    host: "WS01",
    licenseStatus: "Consuming",
    startedAt: "2026-05-20T10:00:00Z",
    durationSeconds: 42,
    ...overrides,
  };
}

// Тестовый хост: tanstack-таблица сеансов как в useSessionsPage (клиентская сортировка),
// чтобы клики по заголовкам переключали состояние и компонент перерисовывался.
function Host({ rows, isAdmin = false }: { rows: SessionSnapshotEntry[]; isAdmin?: boolean }) {
  const [sorting, setSorting] = useState<SortingState>([{ id: "startedAt", desc: true }]);
  const columns = buildSessionColumns({ t: i18n.t, isAdmin, onKill: vi.fn() });
  const table = useReactTable({
    data: rows,
    columns,
    getCoreRowModel: getCoreRowModel(),
    getSortedRowModel: getSortedRowModel(),
    getPaginationRowModel: getPaginationRowModel(),
    state: { sorting },
    onSortingChange: setSorting,
  });
  return (
    <TooltipProvider>
      <SessionsTable
        table={table}
        isLoading={false}
        isError={false}
        isAdmin={isAdmin}
        density="comfortable"
        onToggleDensity={vi.fn()}
      />
    </TooltipProvider>
  );
}

describe("SessionsTable", () => {
  it("показывает реальное имя пользователя как есть", () => {
    render(<Host rows={[row({ userName: "Андрей" })]} />);
    expect(screen.getByText("Андрей")).toBeInTheDocument();
  });

  it("пустое имя пользователя рендерит метку «без пользователя»", () => {
    render(<Host rows={[row({ userName: "" })]} />);
    expect(screen.getByText("без пользователя")).toBeInTheDocument();
  });

  it("клик по заголовку «Клиент» сортирует строки по tenantName (клиентская сортировка)", async () => {
    const user = userEvent.setup();
    render(<Host rows={[row({ tenantName: "Бета" }), row({ tenantName: "Альфа" })]} />);

    // До сортировки порядок исходный (startedAt одинаков): Бета, затем Альфа
    const before = screen
      .getAllByRole("row")
      .slice(1)
      .map((r) => r.textContent ?? "");
    expect(before[0]).toContain("Бета");

    await user.click(screen.getByRole("button", { name: /клиент/i }));

    const after = screen
      .getAllByRole("row")
      .slice(1)
      .map((r) => r.textContent ?? "");
    expect(after[0]).toContain("Альфа");
    expect(after[1]).toContain("Бета");
  });

  it("сортируемый заголовок рендерится как кликабельная кнопка", () => {
    render(<Host rows={[row({})]} />);
    expect(screen.getByRole("button", { name: /клиент/i })).toBeInTheDocument();
  });

  // ADR-48 (MLC-166): трёхсостояние лицензии через StatusBadge.
  it("licenseStatus=Consuming → бейдж «Считается» (success)", () => {
    render(<Host rows={[row({ licenseStatus: "Consuming" })]} />);
    expect(screen.getByText("Считается")).toBeInTheDocument();
  });

  it("licenseStatus=NotConsuming → бейдж «Не считается» (neutral)", () => {
    render(<Host rows={[row({ licenseStatus: "NotConsuming" })]} />);
    expect(screen.getByText("Не считается")).toBeInTheDocument();
  });

  it("licenseStatus=Pending → бейдж «Определяется…» (info)", () => {
    render(<Host rows={[row({ licenseStatus: "Pending" })]} />);
    expect(screen.getByText("Определяется…")).toBeInTheDocument();
  });
});
