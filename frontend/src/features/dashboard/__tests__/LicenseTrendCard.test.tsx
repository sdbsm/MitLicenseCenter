import { render, screen } from "@testing-library/react";
import { describe, expect, it } from "vitest";
import "@/i18n";
import { LicenseTrendCard } from "../LicenseTrendCard";
import type { LicenseUsageBucketPoint } from "@/features/reports/types";

const buckets: LicenseUsageBucketPoint[] = [
  { bucketStartUtc: "2026-06-10T00:00:00Z", consumedAvg: 2, consumedMax: 4, limit: 10 },
  { bucketStartUtc: "2026-06-11T00:00:00Z", consumedAvg: 3, consumedMax: 6, limit: 10 },
];

describe("LicenseTrendCard (MLC-186c)", () => {
  it("показывает заголовок и стат пика при наличии данных", () => {
    render(
      <LicenseTrendCard buckets={buckets} peakConsumed={6} peakLimit={10} isLoading={false} />
    );
    expect(screen.getByText("Использование лицензий (7 дней)")).toBeInTheDocument();
    expect(screen.getByText("Пик: 6 из 10")).toBeInTheDocument();
  });

  it("пустой ряд → заголовок + текст «нет данных», без стата пика", () => {
    render(<LicenseTrendCard buckets={[]} peakConsumed={0} peakLimit={0} isLoading={false} />);
    expect(screen.getByText("Использование лицензий (7 дней)")).toBeInTheDocument();
    expect(screen.getByText("Нет данных за период")).toBeInTheDocument();
    expect(screen.queryByText(/Пик:/)).not.toBeInTheDocument();
  });

  it("загрузка → скелетон вместо графика/стата", () => {
    const { container } = render(
      <LicenseTrendCard
        buckets={undefined}
        peakConsumed={undefined}
        peakLimit={undefined}
        isLoading
      />
    );
    expect(screen.getByText("Использование лицензий (7 дней)")).toBeInTheDocument();
    expect(screen.queryByText(/Пик:/)).not.toBeInTheDocument();
    expect(screen.queryByText("Нет данных за период")).not.toBeInTheDocument();
    expect(container.querySelector('[data-slot="skeleton"]')).toBeInTheDocument();
  });
});
