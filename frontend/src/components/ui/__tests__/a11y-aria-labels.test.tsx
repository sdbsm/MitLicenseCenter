/**
 * MLC-138 / UX-40: проверка русских aria-label в ключевых компонентах.
 *
 * Цель — зафиксировать, что после перевода (UX-40) aria-label не вернулись к английским.
 * Тесты поведенческие: проверяют присутствие русского текста, не Tailwind-классы.
 *
 * Охват: Pagination (nav + кнопки Назад/Вперёд), Dialog (кнопка Закрыть sr-only).
 * SidebarRail не тестируется изолированно — требует полного SidebarProvider/контекста.
 */
import { describe, it, expect } from "vitest";
import { render, screen } from "@testing-library/react";
import "@/i18n";
import {
  Pagination,
  PaginationContent,
  PaginationItem,
  PaginationPrevious,
  PaginationNext,
  PaginationEllipsis,
} from "../pagination";
import { Dialog, DialogContent } from "../dialog";

// --- Pagination ---

describe("Pagination — русские aria-label (UX-40)", () => {
  it("nav имеет aria-label='Пагинация' (не английский)", () => {
    render(
      <Pagination>
        <PaginationContent />
      </Pagination>,
    );
    const nav = screen.getByRole("navigation");
    expect(nav).toHaveAttribute("aria-label", "Пагинация");
    expect(nav).not.toHaveAttribute("aria-label", "pagination");
  });

  it("PaginationPrevious имеет русский видимый текст 'Назад'", () => {
    render(
      <Pagination>
        <PaginationContent>
          <PaginationItem>
            <PaginationPrevious />
          </PaginationItem>
        </PaginationContent>
      </Pagination>,
    );
    // Текст скрыт на мобильных, но присутствует в DOM
    expect(screen.getByText("Назад")).toBeInTheDocument();
  });

  it("PaginationNext имеет русский видимый текст 'Вперёд'", () => {
    render(
      <Pagination>
        <PaginationContent>
          <PaginationItem>
            <PaginationNext />
          </PaginationItem>
        </PaginationContent>
      </Pagination>,
    );
    expect(screen.getByText("Вперёд")).toBeInTheDocument();
  });

  it("PaginationEllipsis sr-only содержит русский текст", () => {
    render(
      <Pagination>
        <PaginationContent>
          <PaginationItem>
            <PaginationEllipsis />
          </PaginationItem>
        </PaginationContent>
      </Pagination>,
    );
    expect(screen.getByText("Ещё страницы")).toBeInTheDocument();
  });
});

// --- Dialog close button ---

describe("DialogContent — русский sr-only текст кнопки закрытия (UX-40)", () => {
  it("кнопка закрытия содержит sr-only 'Закрыть', а не 'Close'", () => {
    render(
      <Dialog open>
        <DialogContent>
          <p>Содержимое диалога</p>
        </DialogContent>
      </Dialog>,
    );
    // Кнопка закрытия содержит sr-only текст «Закрыть»
    expect(screen.getByText("Закрыть")).toBeInTheDocument();
    expect(screen.queryByText("Close")).not.toBeInTheDocument();
  });
});
