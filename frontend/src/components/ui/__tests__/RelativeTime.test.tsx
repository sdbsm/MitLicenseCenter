import { describe, it, expect, beforeEach, afterEach, vi } from "vitest";
import { act, render, screen } from "@testing-library/react";
import { RelativeTime } from "../RelativeTime";

describe("RelativeTime", () => {
  beforeEach(() => {
    vi.useFakeTimers();
    vi.setSystemTime(new Date("2026-05-23T12:00:00Z"));
  });

  afterEach(() => {
    vi.useRealTimers();
  });

  it("renders Russian relative time for a recent timestamp", () => {
    const value = new Date("2026-05-23T11:59:55Z").toISOString(); // 5s ago
    render(<RelativeTime value={value} />);
    expect(screen.getByText(/секунд/)).toBeInTheDocument();
  });

  it("updates after the 1-second interval fires", () => {
    const value = new Date("2026-05-23T11:59:55Z").toISOString(); // 5s ago
    render(<RelativeTime value={value} />);
    const initialText = screen.getByText(/секунд/).textContent;
    act(() => {
      vi.advanceTimersByTime(3_000);
    });
    const newText = screen.getByText(/секунд/).textContent;
    expect(newText).not.toBe(initialText);
  });

  it("applies the amber class once diffSec exceeds thresholdAmberSec", () => {
    const value = new Date("2026-05-23T11:58:00Z").toISOString(); // 120s ago
    render(<RelativeTime value={value} thresholdAmberSec={60} />);
    const el = screen.getByText(/минут/);
    expect(el.className).toContain("text-amber-600");
  });

  it("applies the destructive class when isError is true", () => {
    const value = new Date("2026-05-23T11:59:55Z").toISOString();
    render(<RelativeTime value={value} isError />);
    const el = screen.getByText(/секунд/);
    expect(el.className).toContain("text-destructive");
  });
});
