// @vitest-environment node
// Smoke-тест toInvestigationPdf (MLC-244): node-окружение (нет canvas), текстовый PDF
// без графика — картинки нет в принципе. Паттерн из reports/export/__tests__/toPdf.test.ts.
import { describe, expect, it } from "vitest";
import type { InvestigationReport, CollectionConfig } from "../types";
import { toInvestigationPdf } from "../toInvestigationPdf";

const makeSummary = () => ({
  id: "aabbccdd-1111-2222-3333-444455556666",
  scenario: "Locks" as const,
  status: "Completed" as const,
  startedAtUtc: "2026-06-21T10:00:00Z",
  stoppedAtUtc: "2026-06-21T10:10:00Z",
  startedBy: "operator",
  stopReason: "Manual" as const,
  tenantId: null,
  infobaseId: null,
  findingsCount: 1,
});

const sampleReport: InvestigationReport = {
  summary: makeSummary(),
  generatedAtUtc: "2026-06-21T11:00:00Z",
  items: [
    {
      kind: "ManagedLocks",
      severity: "Warning",
      count: 3,
      headline: "Обнаружены блокировки управляемого уровня",
      recommendation: "Разберите цепочки ожидания в 1С.",
    },
  ],
};

const sampleConfig: CollectionConfig = {
  logcfgLocation: "C:\\logcfg\\logcfg.xml",
  events: "TLOCK,TTIMEOUT,TDEADLOCK",
  durationThresholdMicros: 5_000_000,
  processNameFilter: "mitpro",
  format: "json",
  historyHours: 24,
};

describe("toInvestigationPdf", () => {
  it("создаёт непустой PDF-blob с сигнатурой %PDF", async () => {
    const blob = await toInvestigationPdf(sampleReport, sampleConfig, "mitpro");
    expect(blob.type).toBe("application/pdf");
    expect(blob.size).toBeGreaterThan(0);
    const head = new Uint8Array(await blob.slice(0, 4).arrayBuffer());
    expect(String.fromCharCode(...head)).toBe("%PDF");
  });

  it("работает без collectionConfig (null) — историческое дело", async () => {
    const blob = await toInvestigationPdf(sampleReport, null, null);
    expect(blob.type).toBe("application/pdf");
    expect(blob.size).toBeGreaterThan(0);
  });

  it("работает с пустым items[] — нет находок", async () => {
    const emptyReport: InvestigationReport = { ...sampleReport, items: [] };
    const blob = await toInvestigationPdf(emptyReport, sampleConfig, null);
    expect(blob.type).toBe("application/pdf");
    expect(blob.size).toBeGreaterThan(0);
  });

  it("без durationThresholdMicros и processNameFilter — не падает", async () => {
    const configMinimal: CollectionConfig = {
      logcfgLocation: "C:\\logcfg\\logcfg.xml",
      events: "TLOCK",
      durationThresholdMicros: null,
      processNameFilter: null,
      format: "json",
      historyHours: 1,
    };
    const blob = await toInvestigationPdf(sampleReport, configMinimal, null);
    expect(blob.type).toBe("application/pdf");
    expect(blob.size).toBeGreaterThan(0);
  });
});
