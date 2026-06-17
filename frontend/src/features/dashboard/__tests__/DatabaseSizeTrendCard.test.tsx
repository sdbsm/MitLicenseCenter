import { render, screen } from "@testing-library/react";
import { describe, expect, it } from "vitest";
import "@/i18n";
import { DatabaseSizeTrendCard } from "../DatabaseSizeTrendCard";
import type { DatabaseSizePoint } from "@/features/reports/types";

const GB = 1024 ** 3;

function point(atUtc: string, totalBytes: number): DatabaseSizePoint {
  return { atUtc, dataBytes: totalBytes, logBytes: 0, totalBytes };
}

describe("DatabaseSizeTrendCard (MLC-186c)", () => {
  it("дельта за неделю = разница последней и первой точки (рост, знак +)", () => {
    const points = [point("2026-06-10T00:00:00Z", 10 * GB), point("2026-06-17T00:00:00Z", 13 * GB)];
    render(<DatabaseSizeTrendCard points={points} isLoading={false} />);
    expect(screen.getByText("Рост размера баз")).toBeInTheDocument();
    expect(screen.getByText("Δ за неделю: +3.0 ГБ")).toBeInTheDocument();
  });

  it("уменьшение → знак −", () => {
    const points = [point("2026-06-10T00:00:00Z", 13 * GB), point("2026-06-17T00:00:00Z", 10 * GB)];
    render(<DatabaseSizeTrendCard points={points} isLoading={false} />);
    expect(screen.getByText("Δ за неделю: −3.0 ГБ")).toBeInTheDocument();
  });

  it("<2 точек → дельта «—»", () => {
    render(
      <DatabaseSizeTrendCard points={[point("2026-06-17T00:00:00Z", 5 * GB)]} isLoading={false} />
    );
    expect(screen.getByText("Δ за неделю: —")).toBeInTheDocument();
  });

  it("пустой ряд → текст «нет данных», без стата дельты", () => {
    render(<DatabaseSizeTrendCard points={[]} isLoading={false} />);
    expect(screen.getByText("Рост размера баз")).toBeInTheDocument();
    expect(screen.getByText("Нет данных за период")).toBeInTheDocument();
    expect(screen.queryByText(/Δ за неделю/)).not.toBeInTheDocument();
  });

  it("загрузка → скелетон", () => {
    const { container } = render(<DatabaseSizeTrendCard points={undefined} isLoading />);
    expect(screen.getByText("Рост размера баз")).toBeInTheDocument();
    expect(container.querySelector('[data-slot="skeleton"]')).toBeInTheDocument();
  });
});
