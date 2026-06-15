import { describe, it, expect, vi } from "vitest";
import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { useState } from "react";
import "@/i18n";
import { SearchableMultiSelect } from "../SearchableMultiSelect";
import type { SearchableSelectOption } from "../SearchableSelect";

const OPTIONS: SearchableSelectOption[] = [
  { value: "a", label: "Альфа" },
  { value: "b", label: "Бета" },
  { value: "c", label: "Гамма" },
];

// Управляемый хост: держит value в state, чтобы тоггл-кнопки перерисовывали список.
function Host({ initial = [] as string[] }) {
  const [value, setValue] = useState<string[]>(initial);
  return (
    <>
      <SearchableMultiSelect
        options={OPTIONS}
        value={value}
        onChange={setValue}
        placeholder="Все"
        selectedLabel={(n) => `Выбрано: ${n}`}
        aria-label="Типы"
      />
      <output data-testid="value">{value.join(",")}</output>
    </>
  );
}

async function openPopover(user: ReturnType<typeof userEvent.setup>) {
  await user.click(screen.getByRole("combobox", { name: "Типы" }));
}

describe("SearchableMultiSelect — Выбрать все / Снять все (MLC-167)", () => {
  it("«Выбрать все» отмечает все опции текущего списка", async () => {
    const user = userEvent.setup();
    render(<Host />);
    await openPopover(user);

    await user.click(screen.getByRole("button", { name: "Выбрать все" }));
    expect(screen.getByTestId("value").textContent).toBe("a,b,c");
  });

  it("когда всё выбрано — действие становится «Снять все» и очищает выбор", async () => {
    const user = userEvent.setup();
    render(<Host initial={["a", "b", "c"]} />);
    await openPopover(user);

    await user.click(screen.getByRole("button", { name: "Снять все" }));
    expect(screen.getByTestId("value").textContent).toBe("");
  });

  it("«Выбрать все» под фильтром поиска влияет только на видимые опции, сохраняя прочий выбор", async () => {
    const user = userEvent.setup();
    render(<Host initial={["c"]} />);
    await openPopover(user);

    // Фильтруем до «Альфа» — видна одна опция; «Гамма» (c) вне фильтра должна сохраниться.
    await user.type(screen.getByRole("textbox"), "Альф");
    await user.click(screen.getByRole("button", { name: "Выбрать все" }));

    const value = screen.getByTestId("value").textContent ?? "";
    expect(value.split(",").sort()).toEqual(["a", "c"]);
  });

  it("одиночный тоггл опции по-прежнему работает (не сломан)", async () => {
    const user = userEvent.setup();
    const onChange = vi.fn();
    render(
      <SearchableMultiSelect
        options={OPTIONS}
        value={[]}
        onChange={onChange}
        placeholder="Все"
        aria-label="Типы"
      />
    );
    await user.click(screen.getByRole("combobox", { name: "Типы" }));
    await user.click(screen.getByRole("option", { name: "Бета" }));
    expect(onChange).toHaveBeenCalledWith(["b"]);
  });
});
