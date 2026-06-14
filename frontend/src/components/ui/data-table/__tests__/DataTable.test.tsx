import { describe, it, expect, beforeEach } from "vitest";
import { render, screen, act } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { getCoreRowModel, useReactTable, type ColumnDef } from "@tanstack/react-table";
import { MemoryRouter, useSearchParams } from "react-router";
import "@/i18n";
import { DataTable } from "../DataTable";
import { useTableDensity } from "../useTableDensity";
import { useUrlTableFilters } from "../useUrlTableFilters";

interface Item {
  name: string;
  city: string;
}

const DATA: Item[] = [
  { name: "Альфа", city: "Москва" },
  { name: "Бета", city: "Казань" },
];

const COLUMNS: ColumnDef<Item>[] = [
  { id: "name", accessorKey: "name", header: "Название", meta: { label: "Название" } },
  { id: "city", accessorKey: "city", header: "Город", meta: { label: "Город" } },
];

function Host() {
  const { density, toggleDensity } = useTableDensity();
  const table = useReactTable({
    data: DATA,
    columns: COLUMNS,
    getCoreRowModel: getCoreRowModel(),
  });
  return (
    <DataTable
      table={table}
      density={density}
      onToggleDensity={toggleDensity}
      columnLabel={(id) => table.getColumn(id)?.columnDef.meta?.label ?? id}
    />
  );
}

beforeEach(() => {
  window.localStorage.clear();
});

describe("DataTable — видимость колонок", () => {
  it("снятие чекбокса в меню «Колонки» скрывает столбец", async () => {
    const user = userEvent.setup();
    render(<Host />);

    // Колонка «Город» видна
    expect(screen.getByRole("columnheader", { name: "Город" })).toBeInTheDocument();

    await user.click(screen.getByRole("button", { name: /колонки/i }));
    // Чекбокс «Город» в меню — снимаем
    const cityToggle = await screen.findByRole("menuitemcheckbox", { name: "Город" });
    await user.click(cityToggle);
    // Закрываем меню, чтобы портал не перекрывал дерево таблицы при проверке.
    await user.keyboard("{Escape}");

    expect(screen.queryByRole("columnheader", { name: "Город" })).toBeNull();
    // Колонка «Название» осталась
    expect(screen.getByRole("columnheader", { name: "Название" })).toBeInTheDocument();
  });
});

describe("DataTable — density-toggle", () => {
  it("дефолт comfortable; переключение пишет compact в localStorage и меняет паддинг ячеек", async () => {
    const user = userEvent.setup();
    const { container } = render(<Host />);

    // Дефолт — comfortable: ячейки с py-2
    const cell = container.querySelector("td");
    expect(cell?.className).toContain("py-2");

    await user.click(screen.getByRole("button", { name: /компактно/i }));

    expect(window.localStorage.getItem("mlc-table-density")).toBe("compact");
    const compactCell = container.querySelector("td");
    expect(compactCell?.className).toContain("py-1");
  });

  it("читает сохранённую плотность из localStorage при монтировании", () => {
    window.localStorage.setItem("mlc-table-density", "compact");
    const { container } = render(<Host />);
    expect(container.querySelector("td")?.className).toContain("py-1");
  });
});

describe("useUrlTableFilters — сериализация фильтра в URL", () => {
  function FilterHost() {
    const { columnFilters, onColumnFiltersChange } = useUrlTableFilters();
    const [params] = useSearchParams();
    return (
      <div>
        <span data-testid="url">{params.toString()}</span>
        <span data-testid="filters">{JSON.stringify(columnFilters)}</span>
        <button onClick={() => onColumnFiltersChange([{ id: "name", value: "ром" }])}>set</button>
        <button onClick={() => onColumnFiltersChange([])}>clear</button>
      </div>
    );
  }

  it("смена фильтра колонки отражается в URL (?f_name=…) и обратно читается", async () => {
    const user = userEvent.setup();
    render(
      <MemoryRouter>
        <FilterHost />
      </MemoryRouter>
    );

    await user.click(screen.getByText("set"));
    expect(screen.getByTestId("url").textContent).toContain("f_name=");
    expect(screen.getByTestId("filters").textContent).toContain('"id":"name"');

    await user.click(screen.getByText("clear"));
    expect(screen.getByTestId("url").textContent).not.toContain("f_name");
  });

  it("не трогает чужие URL-параметры без префикса f_", async () => {
    const user = userEvent.setup();

    function Outer() {
      const [, setParams] = useSearchParams();
      return (
        <div>
          <button onClick={() => setParams({ page: "2" })}>seed</button>
          <FilterHost />
        </div>
      );
    }

    render(
      <MemoryRouter>
        <Outer />
      </MemoryRouter>
    );

    await act(async () => {
      await user.click(screen.getByText("seed"));
    });
    await user.click(screen.getByText("set"));

    const url = screen.getByTestId("url").textContent ?? "";
    expect(url).toContain("page=2");
    expect(url).toContain("f_name=");
  });
});
