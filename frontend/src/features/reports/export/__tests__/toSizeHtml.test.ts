// @vitest-environment node
// Чистый сериалайзер строки: node-окружение даёт Blob с .text() (jsdom-Blob не имеет).
import { describe, expect, it } from "vitest";
import { toSizeHtml } from "../toSizeHtml";
import type { SizeExportData } from "../sizeExport";

const sample: SizeExportData = {
  scope: "all",
  points: [
    { atUtc: "2026-06-01T12:00:00Z", dataBytes: 1000, logBytes: 200, totalBytes: 1200 },
    { atUtc: "2026-06-02T12:00:00Z", dataBytes: 1500, logBytes: 300, totalBytes: 1800 },
  ],
  fromUtc: "2026-06-01T12:00:00Z",
  toUtc: "2026-06-07T12:00:00Z",
  tenants: [],
  databases: [],
};

describe("toSizeHtml", () => {
  it("produces a text/html blob", () => {
    expect(toSizeHtml(sample).type).toContain("text/html");
  });

  it("inlines the Chart.js engine and a canvas + init script (offline, no CDN)", async () => {
    const html = await toSizeHtml(sample).text();
    expect(html).toContain("Chart.js v");
    expect(html).toContain('<canvas id="chart">');
    expect(html).toContain('new Chart(document.getElementById("chart")');
  });

  it("embeds the size series as JSON and a byte formatter", async () => {
    const html = await toSizeHtml(sample).text();
    expect(html).toContain("var DATA = ");
    expect(html).toContain('"Общий размер"');
    expect(html).toContain("function formatBytes");
  });

  it("carries the title + period but no raw table", async () => {
    const html = await toSizeHtml(sample).text();
    expect(html).toContain("Размер баз — Все клиенты");
    expect(html).toContain("Период:");
    expect(html).not.toContain("<table");
  });

  it("labels the tenant scope in the title", async () => {
    const html = await toSizeHtml({ ...sample, scope: { tenantName: "ООО Ромашка" } }).text();
    expect(html).toContain("Размер баз — ООО Ромашка");
  });

  it("handles an empty series (canvas present, still valid HTML)", async () => {
    const html = await toSizeHtml({ ...sample, points: [] }).text();
    expect(html).toContain("<canvas");
    expect(html).not.toContain("<table");
  });
});
