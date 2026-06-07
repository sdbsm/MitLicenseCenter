// @vitest-environment node
// Лёгкий smoke: node-окружение (нет canvas-контекста) → картинка графика
// пропускается, проверяем сам PDF-документ (заголовок/сводка/таблица + кириллический
// шрифт строятся без canvas). Blob.arrayBuffer() есть в node.
import { describe, expect, it } from "vitest";
import type { LicenseUsageSeriesResponse } from "../../types";
import { toPdf } from "../toPdf";

const sample: LicenseUsageSeriesResponse = {
  buckets: [
    { bucketStartUtc: "2026-06-01T12:00:00Z", consumedAvg: 3.46, consumedMax: 5, limit: 10 },
    { bucketStartUtc: "2026-06-01T12:15:00Z", consumedAvg: 4, consumedMax: 6, limit: 10 },
  ],
  fromUtc: "2026-06-01T12:00:00Z",
  toUtc: "2026-06-07T12:00:00Z",
  peakConsumed: 6,
  peakLimit: 10,
  peakAtUtc: "2026-06-01T12:15:00Z",
  averageConsumed: 3.73,
};

describe("toPdf", () => {
  it("produces a non-empty application/pdf blob", async () => {
    const blob = await toPdf(sample, "all");
    expect(blob.type).toBe("application/pdf");
    expect(blob.size).toBeGreaterThan(0);
    // Сигнатура PDF — заголовок «%PDF» в первых байтах.
    const head = new Uint8Array(await blob.slice(0, 4).arrayBuffer());
    expect(String.fromCharCode(...head)).toBe("%PDF");
  });

  it("builds a valid PDF for an empty series too", async () => {
    const blob = await toPdf({ ...sample, buckets: [] }, { tenantName: "ООО Ромашка" });
    expect(blob.type).toBe("application/pdf");
    expect(blob.size).toBeGreaterThan(0);
  });
});
