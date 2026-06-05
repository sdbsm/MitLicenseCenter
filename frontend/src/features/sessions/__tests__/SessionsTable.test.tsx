import { describe, it, expect, vi } from "vitest";
import { render, screen } from "@testing-library/react";
import type { ReactNode } from "react";
import "@/i18n";
import { TooltipProvider } from "@/components/ui/tooltip";
import { SessionsTable } from "../SessionsTable";
import type { SessionSnapshotEntry } from "../types";

function renderTable(ui: ReactNode) {
  return render(<TooltipProvider>{ui}</TooltipProvider>);
}

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
    consumesLicense: true,
    startedAt: "2026-05-20T10:00:00Z",
    durationSeconds: 42,
    ...overrides,
  };
}

describe("SessionsTable", () => {
  it("показывает реальное имя пользователя как есть", () => {
    renderTable(
      <SessionsTable
        rows={[row({ sessionId: "aaaa1111-1111-1111-1111-111111111111", userName: "Андрей" })]}
        isLoading={false}
        isError={false}
        isAdmin={false}
        onKill={vi.fn()}
      />
    );

    expect(screen.getByText("Андрей")).toBeInTheDocument();
  });

  it("пустое имя пользователя рендерит метку «без пользователя»", () => {
    renderTable(
      <SessionsTable
        rows={[row({ sessionId: "bbbb2222-2222-2222-2222-222222222222", userName: "" })]}
        isLoading={false}
        isError={false}
        isAdmin={false}
        onKill={vi.fn()}
      />
    );

    expect(screen.getByText("без пользователя")).toBeInTheDocument();
  });
});
