import { render, screen } from "@testing-library/react";
import { MemoryRouter } from "react-router";
import { beforeEach, describe, expect, it, vi } from "vitest";
import "@/i18n";
import { RecentActivityCard } from "../RecentActivityCard";
import type { AuditEntry, AuditPagedResponse } from "@/features/audit/types";

// Лента «Свежая активность» тянет useAuditLog (последние записи журнала). Мокаем
// хук напрямую — тест сфокусирован на рендере строк/состояний, а не на сети.
const auditState: {
  data: AuditPagedResponse | undefined;
  isLoading: boolean;
  isError: boolean;
} = { data: undefined, isLoading: false, isError: false };

vi.mock("@/features/audit/useAuditLog", () => ({
  useAuditLog: () => auditState,
}));

function entry(over: Partial<AuditEntry>): AuditEntry {
  return {
    id: "a-1",
    timestamp: new Date().toISOString(),
    actionType: "TenantCreated",
    reason: null,
    initiator: "admin",
    description: "Описание события",
    tenantId: null,
    ...over,
  };
}

function renderCard() {
  return render(
    <MemoryRouter>
      <RecentActivityCard />
    </MemoryRouter>
  );
}

describe("RecentActivityCard (MLC-186d)", () => {
  beforeEach(() => {
    auditState.data = undefined;
    auditState.isLoading = false;
    auditState.isError = false;
  });

  it("рендерит строки: метка действия + описание + относительное время", () => {
    auditState.data = {
      items: [
        entry({ id: "a-1", actionType: "TenantCreated", description: "Клиент Ромашка" }),
        entry({ id: "a-2", actionType: "AdminLoggedIn", description: "Вход admin" }),
      ],
      total: 2,
      page: 1,
      pageSize: 25,
    };
    renderCard();

    // Локализованные метки действий (тот же ключ audit.actions.*, что и в таблице).
    expect(screen.getByText("Клиент создан")).toBeInTheDocument();
    expect(screen.getByText("Вход администратора")).toBeInTheDocument();
    // Описания событий.
    expect(screen.getByText("Клиент Ромашка")).toBeInTheDocument();
    expect(screen.getByText("Вход admin")).toBeInTheDocument();
    // Относительное время (RelativeTime для свежей метки → «сейчас»).
    expect(screen.getAllByText("сейчас").length).toBeGreaterThan(0);
  });

  it("показывает не более 5 записей (срез на клиенте)", () => {
    auditState.data = {
      items: Array.from({ length: 8 }).map((_, i) =>
        entry({ id: `a-${i}`, description: `Событие ${i}` })
      ),
      total: 8,
      page: 1,
      pageSize: 25,
    };
    renderCard();
    expect(screen.getByText("Событие 4")).toBeInTheDocument();
    expect(screen.queryByText("Событие 5")).not.toBeInTheDocument();
  });

  it("пусто → muted-текст empty-state", () => {
    auditState.data = { items: [], total: 0, page: 1, pageSize: 25 };
    renderCard();
    expect(screen.getByText("Нет недавних событий")).toBeInTheDocument();
  });

  it("загрузка → скелетоны", () => {
    auditState.isLoading = true;
    const { container } = renderCard();
    expect(container.querySelectorAll('[data-slot="skeleton"]').length).toBe(5);
  });

  it("«Показать всё» ведёт на /audit", () => {
    auditState.data = { items: [], total: 0, page: 1, pageSize: 25 };
    renderCard();
    expect(screen.getByText("Показать всё").closest("a")).toHaveAttribute("href", "/audit");
  });
});
