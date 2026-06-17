// @vitest-environment node
// Чистый сериалайзер без DOM: node-окружение даёт Blob с .arrayBuffer() (jsdom-Blob не имеет).
import * as XLSX from "xlsx";
import { describe, expect, it } from "vitest";
import { toSizeXlsx } from "../toSizeXlsx";
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

const detail: SizeExportData = {
  scope: { tenantName: "ООО Ромашка" },
  points: summary.points,
  fromUtc: summary.fromUtc,
  toUtc: summary.toUtc,
  tenants: [],
  databases: [{ databaseName: "acc_main", dataBytes: 1400, logBytes: 290, totalBytes: 1690 }],
};

async function readBack(blob: Blob): Promise<XLSX.WorkBook> {
  return XLSX.read(await blob.arrayBuffer(), { type: "array" });
}

describe("toSizeXlsx", () => {
  it("produces «Сводка», «Рост размера» and «Клиенты» sheets for the summary", async () => {
    const wb = await readBack(await toSizeXlsx(summary));
    expect(wb.SheetNames).toEqual(["Сводка", "Рост размера", "Клиенты"]);
  });

  it("produces a «Базы» sheet for the detail scope", async () => {
    const wb = await readBack(await toSizeXlsx(detail));
    expect(wb.SheetNames).toEqual(["Сводка", "Рост размера", "Базы"]);
  });

  it("labels the scope on the «Сводка» sheet", async () => {
    const wb = await readBack(await toSizeXlsx(summary));
    expect(wb.Sheets["Сводка"].A1.v).toBe("Разрез");
    expect(wb.Sheets["Сводка"].B1.v).toBe("Все клиенты");

    const tenantWb = await readBack(await toSizeXlsx(detail));
    expect(tenantWb.Sheets["Сводка"].B1.v).toBe("ООО Ромашка");
  });

  it("stores byte cells as real numbers on «Рост размера»", async () => {
    const wb = await readBack(await toSizeXlsx(summary));
    const sheet = wb.Sheets["Рост размера"];
    expect(sheet.A1.v).toBe("Момент");
    expect(sheet.D1.v).toBe("Итого (байт)");
    expect(sheet.B2.t).toBe("n");
    expect(sheet.B2.v).toBe(1000);
    expect(sheet.D2.v).toBe(1200);
  });

  it("writes the tenant breakdown with the database count", async () => {
    const wb = await readBack(await toSizeXlsx(summary));
    const sheet = wb.Sheets["Клиенты"];
    expect(sheet.A1.v).toBe("Клиент");
    expect(sheet.A2.v).toBe("ООО Ромашка");
    expect(sheet.B2.v).toBe(1800);
    expect(sheet.E2.v).toBe(2);
  });
});
