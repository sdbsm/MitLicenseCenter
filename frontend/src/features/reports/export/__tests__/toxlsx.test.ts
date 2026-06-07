// @vitest-environment node
// Чистый сериалайзер без DOM: node-окружение даёт Blob с .arrayBuffer() (jsdom-Blob не имеет).
import * as XLSX from "xlsx";
import { describe, expect, it } from "vitest";
import type { LicenseUsageSeriesResponse } from "../../types";
import { toXlsx } from "../toXlsx";

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

async function readBack(blob: Blob): Promise<XLSX.WorkBook> {
  const buffer = await blob.arrayBuffer();
  return XLSX.read(buffer, { type: "array" });
}

describe("toXlsx", () => {
  it("produces a workbook with «Сводка» and «Данные» sheets", async () => {
    const wb = await readBack(await toXlsx(sample, "all"));
    expect(wb.SheetNames).toEqual(["Сводка", "Данные"]);
  });

  it("writes the data header row on the «Данные» sheet", async () => {
    const wb = await readBack(await toXlsx(sample, "all"));
    const sheet = wb.Sheets["Данные"];
    expect(sheet.A1.v).toBe("Начало бакета");
    expect(sheet.B1.v).toBe("Среднее");
    expect(sheet.C1.v).toBe("Пик");
    expect(sheet.D1.v).toBe("Лимит");
  });

  it("stores numeric cells as real numbers (not strings)", async () => {
    const wb = await readBack(await toXlsx(sample, "all"));
    const sheet = wb.Sheets["Данные"];
    // Первая строка данных: B2 среднее (3.46→3.5), C2 пик, D2 лимит — тип «n».
    expect(sheet.B2.t).toBe("n");
    expect(sheet.B2.v).toBe(3.5);
    expect(sheet.C2.t).toBe("n");
    expect(sheet.C2.v).toBe(5);
    expect(sheet.D2.t).toBe("n");
    expect(sheet.D2.v).toBe(10);
  });

  it("labels the scope on the «Сводка» sheet", async () => {
    const allWb = await readBack(await toXlsx(sample, "all"));
    expect(allWb.Sheets["Сводка"].A1.v).toBe("Разрез");
    expect(allWb.Sheets["Сводка"].B1.v).toBe("Все клиенты");

    const tenantWb = await readBack(await toXlsx(sample, { tenantName: "ООО Ромашка" }));
    expect(tenantWb.Sheets["Сводка"].B1.v).toBe("ООО Ромашка");
  });
});
