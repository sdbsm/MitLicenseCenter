// @vitest-environment node
// Лёгкий smoke: node-окружение (нет canvas-контекста) → картинка графика пропускается,
// проверяем сам PDF-документ (заголовок/таблица + кириллический шрифт без canvas).
import { describe, expect, it } from "vitest";
import { toSizePdf } from "../toSizePdf";
import type { SizeExportData } from "../sizeExport";

const summary: SizeExportData = {
  scope: "all",
  points: [{ atUtc: "2026-06-01T12:00:00Z", dataBytes: 1000, logBytes: 200, totalBytes: 1200 }],
  fromUtc: "2026-06-01T12:00:00Z",
  toUtc: "2026-06-07T12:00:00Z",
  tenants: [
    {
      tenantId: "t1",
      tenantName: "ООО Ромашка",
      dataBytes: 1500,
      logBytes: 300,
      totalBytes: 1800,
      databaseCount: 2,
    },
  ],
  databases: [],
};

describe("toSizePdf", () => {
  it("produces a non-empty application/pdf blob", async () => {
    const blob = await toSizePdf(summary);
    expect(blob.type).toBe("application/pdf");
    expect(blob.size).toBeGreaterThan(0);
    const head = new Uint8Array(await blob.slice(0, 4).arrayBuffer());
    expect(String.fromCharCode(...head)).toBe("%PDF");
  });

  it("builds a valid PDF for the detail scope and an empty series too", async () => {
    const blob = await toSizePdf({
      ...summary,
      scope: { tenantName: "ООО Ромашка" },
      points: [],
      tenants: [],
      databases: [{ databaseName: "acc_main", dataBytes: 1, logBytes: 1, totalBytes: 2 }],
    });
    expect(blob.type).toBe("application/pdf");
    expect(blob.size).toBeGreaterThan(0);
  });
});
