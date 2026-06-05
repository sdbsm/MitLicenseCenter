import { describe, it, expect } from "vitest";
import { render, screen, fireEvent } from "@testing-library/react";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { useState, type ReactNode } from "react";
import "@/i18n";
import { SettingField } from "../SettingField";
import type { SettingDescriptor } from "../types";

// Регрессия на затирание OneC.RAS.ExePath в null:
// SettingField держит локальный `draft`, инициализируемый из setting.value ОДИН раз.
// Когда соседний RacPathDetect сохранял путь, список настроек рефетчился и сюда
// приходил новый setting.value, но draft оставался старым (пустым) → поле показывало
// пусто при активном Save, и клик слал пустой draft → значение стиралось.
// После фикса смена setting.value ресинхронизирует draft.

function descriptor(value: string | null): SettingDescriptor {
  return {
    key: "OneC.RAS.ExePath",
    isSecret: false,
    isSet: value !== null,
    value,
    description: null,
    updatedAt: "2026-01-01T00:00:00Z",
    updatedBy: "admin",
  };
}

function renderWithClient(ui: ReactNode) {
  const client = new QueryClient({
    defaultOptions: { queries: { retry: false }, mutations: { retry: false } },
  });
  return render(<QueryClientProvider client={client}>{ui}</QueryClientProvider>);
}

describe("SettingField — ресинхронизация draft с серверным значением", () => {
  it("подхватывает новое setting.value, пришедшее извне (фикс затирания RAS.ExePath)", () => {
    // Обёртка имитирует родителя: меняет проп setting.value (как делает рефетч
    // после сохранения соседним RacPathDetect), переиспользуя ТОТ ЖЕ инстанс
    // SettingField (без remount) — именно тут раньше draft оставался пустым.
    function Harness() {
      const [value, setValue] = useState<string | null>(null);
      return (
        <>
          <button type="button" onClick={() => setValue("C:\\1cv8\\rac.exe")}>
            simulate-detect-apply
          </button>
          <SettingField setting={descriptor(value)} inputType="text" />
        </>
      );
    }

    renderWithClient(<Harness />);

    const input = screen.getByLabelText("Путь к rac.exe") as HTMLInputElement;
    expect(input.value).toBe("");

    // Сосед сохранил путь → родитель обновил setting.value.
    fireEvent.click(screen.getByText("simulate-detect-apply"));

    // Поле должно отразить новый путь, а не остаться пустым.
    expect(input.value).toBe("C:\\1cv8\\rac.exe");
    // И раз draft == серверному значению, поле не «грязное» — нет риска
    // случайно отправить пустую строку поверх сохранённого пути.
    expect(input.value).not.toBe("");
  });
});
