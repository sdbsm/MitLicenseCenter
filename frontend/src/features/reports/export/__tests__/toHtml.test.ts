// @vitest-environment node
// Чистый сериалайзер строки: node-окружение даёт Blob с .text() (jsdom-Blob не имеет).
import { describe, expect, it } from "vitest";
import type { LicenseUsageSeriesResponse } from "../../types";
import { toHtml } from "../toHtml";

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
  clamped: false,
  maxSpanDays: 31,
};

describe("toHtml", () => {
  it("produces a text/html blob", () => {
    expect(toHtml(sample, "all").type).toContain("text/html");
  });

  it("inlines the Chart.js engine and a canvas + init script (offline, no CDN)", async () => {
    const html = await toHtml(sample, "all").text();
    expect(html).toContain("Chart.js v"); // банер инлайн-движка
    expect(html).toContain('<canvas id="chart">');
    expect(html).toContain('new Chart(document.getElementById("chart")');
  });

  it("embeds the chart config as JSON (labels + datasets)", async () => {
    const html = await toHtml(sample, "all").text();
    expect(html).toContain("const DATA = ");
    expect(html).toContain('"Пик потребления"');
    expect(html).toContain('"Среднее потребление"');
    expect(html).toContain('"Лимит"');
  });

  it("carries the summary (period + peak/average) but no raw bucket table (MLC-054)", async () => {
    const html = await toHtml(sample, "all").text();
    // Сводка на месте: период, пик, среднее.
    expect(html).toContain("Период:");
    expect(html).toContain("Пик за период");
    expect(html).toContain("Среднее за период");
    // Сырой побакетной таблицы — нет.
    expect(html).not.toContain("<table");
    expect(html).not.toContain("Начало бакета");
    expect(html.match(/<tr><td>/g) ?? []).toHaveLength(0);
  });

  it("labels the scope and shows the overview caveat only for the summary", async () => {
    const all = await toHtml(sample, "all").text();
    expect(all).toContain("Все клиенты");
    expect(all).toContain("обзорная оценка");

    const tenant = await toHtml(sample, { tenantName: "ООО Ромашка" }).text();
    expect(tenant).toContain("ООО Ромашка");
    expect(tenant).not.toContain("обзорная оценка");
  });

  it("handles an empty series (chart canvas present, still valid HTML)", async () => {
    const html = await toHtml({ ...sample, buckets: [] }, "all").text();
    expect(html).toContain("<canvas");
    expect(html).not.toContain("<table");
  });
});
