import { render, screen, act } from "@testing-library/react";
import { afterEach, describe, expect, it } from "vitest";
import { markOffline, markOnline, useIsOffline } from "../connectionStatus";

// MLC-121 (UX-03/FE-05) — module-level store состояния соединения. Проверяем, что
// markOffline/markOnline переключают значение и что подписанный компонент
// перерисовывается (useSyncExternalStore).

function Probe() {
  const offline = useIsOffline();
  return <span>{offline ? "OFFLINE" : "ONLINE"}</span>;
}

describe("connectionStatus store", () => {
  afterEach(() => {
    // Возвращаем стор в исходное состояние между тестами.
    act(() => markOnline());
  });

  it("по умолчанию online", () => {
    render(<Probe />);
    expect(screen.getByText("ONLINE")).toBeInTheDocument();
  });

  it("markOffline → компонент видит offline; markOnline снимает", () => {
    render(<Probe />);

    act(() => markOffline());
    expect(screen.getByText("OFFLINE")).toBeInTheDocument();

    act(() => markOnline());
    expect(screen.getByText("ONLINE")).toBeInTheDocument();
  });
});
