import { describe, it, expect, vi } from "vitest";
import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import "@/i18n";
import { SearchableSelect, type SearchableSelectOption } from "../SearchableSelect";

const OPTIONS: SearchableSelectOption[] = [
  { value: "a", label: "Альфа" },
  { value: "b", label: "Бета" },
  { value: "c", label: "Гамма" },
];

describe("SearchableSelect — disabled (MLC-181b)", () => {
  it("отключённый триггер не открывает поповер и показывает текущий label", async () => {
    const user = userEvent.setup();
    const onChange = vi.fn();
    render(
      <SearchableSelect
        options={OPTIONS}
        value="b"
        onChange={onChange}
        placeholder="Не выбрано"
        disabled
        aria-label="Клиент"
      />
    );

    const trigger = screen.getByRole("combobox", { name: "Клиент" });
    // Текущий label виден на триггере.
    expect(trigger).toHaveTextContent("Бета");
    // Триггер недоступен (атрибут disabled) — поповер не открывается.
    expect(trigger).toBeDisabled();

    await user.click(trigger);

    // Поля фильтра/опций нет — поповер закрыт.
    expect(screen.queryByRole("listbox")).not.toBeInTheDocument();
    expect(onChange).not.toHaveBeenCalled();
  });

  it("без disabled триггер открывается и отдаёт выбор", async () => {
    const user = userEvent.setup();
    const onChange = vi.fn();
    render(
      <SearchableSelect
        options={OPTIONS}
        value={null}
        onChange={onChange}
        placeholder="Не выбрано"
        aria-label="Клиент"
      />
    );

    const trigger = screen.getByRole("combobox", { name: "Клиент" });
    expect(trigger).not.toBeDisabled();
    expect(trigger).toHaveTextContent("Не выбрано");

    await user.click(trigger);
    await user.click(await screen.findByRole("option", { name: "Гамма" }));

    expect(onChange).toHaveBeenCalledWith("c");
  });
});
