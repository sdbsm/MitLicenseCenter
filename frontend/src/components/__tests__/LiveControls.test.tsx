import { describe, it, expect, vi } from "vitest";
import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import "@/i18n";
import { LiveControls } from "../LiveControls";

// MLC-156: общий контрол «живых» страниц (/sessions, /performance) — Пауза/Возобновить
// авто-обновления + «Обновить сейчас». Проверяем наблюдаемый контракт: подпись/иконка
// паузы зависят от isPaused, клики дёргают колбэки, isRefreshing крутит иконку обновления
// и блокирует повторный клик.
describe("LiveControls", () => {
  it("показывает «Пауза» когда не на паузе и вызывает onTogglePause по клику", async () => {
    const user = userEvent.setup();
    const onTogglePause = vi.fn();
    const onRefreshNow = vi.fn();

    render(
      <LiveControls isPaused={false} onTogglePause={onTogglePause} onRefreshNow={onRefreshNow} />
    );

    const pauseBtn = screen.getByRole("button", { name: "Пауза" });
    expect(pauseBtn).toBeInTheDocument();

    await user.click(pauseBtn);
    expect(onTogglePause).toHaveBeenCalledTimes(1);
  });

  // MLC-158: цвет/текст статуса кодируют текущее состояние, не действие.
  it("в живом режиме показывает статус «Авто-обновление», на паузе — «На паузе»", () => {
    const { rerender } = render(
      <LiveControls isPaused={false} onTogglePause={vi.fn()} onRefreshNow={vi.fn()} />
    );
    expect(screen.getByText("Авто-обновление")).toBeInTheDocument();
    expect(screen.queryByText("На паузе")).not.toBeInTheDocument();

    rerender(<LiveControls isPaused={true} onTogglePause={vi.fn()} onRefreshNow={vi.fn()} />);
    expect(screen.getByText("На паузе")).toBeInTheDocument();
    expect(screen.queryByText("Авто-обновление")).not.toBeInTheDocument();
  });

  it("показывает «Возобновить» когда на паузе", () => {
    render(<LiveControls isPaused={true} onTogglePause={vi.fn()} onRefreshNow={vi.fn()} />);

    expect(screen.getByRole("button", { name: "Возобновить" })).toBeInTheDocument();
    expect(screen.queryByRole("button", { name: "Пауза" })).not.toBeInTheDocument();
  });

  it("вызывает onRefreshNow по клику «Обновить сейчас»", async () => {
    const user = userEvent.setup();
    const onRefreshNow = vi.fn();

    render(<LiveControls isPaused={false} onTogglePause={vi.fn()} onRefreshNow={onRefreshNow} />);

    await user.click(screen.getByRole("button", { name: "Обновить сейчас" }));
    expect(onRefreshNow).toHaveBeenCalledTimes(1);
  });

  it("блокирует кнопку обновления при isRefreshing", () => {
    render(
      <LiveControls isPaused={false} onTogglePause={vi.fn()} onRefreshNow={vi.fn()} isRefreshing />
    );

    expect(screen.getByRole("button", { name: "Обновить сейчас" })).toBeDisabled();
  });
});
