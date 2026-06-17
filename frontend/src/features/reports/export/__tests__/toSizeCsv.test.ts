// @vitest-environment node
// Чистый сериалайзер без DOM: node-окружение даёт Blob с .text() (jsdom-Blob не имеет).
import { describe, expect, it } from "vitest";
import { toSizeCsv } from "../toSizeCsv";
import type { SizeExportData } from "../sizeExport";

const BOM = 0xfeff;

const summary: SizeExportData = {
  scope: "all",
  points: [
    { atUtc: "2026-06-01T12:00:00Z", dataBytes: 1000, logBytes: 200, totalBytes: 1200 },
    { atUtc: "2026-06-02T12:00:00Z", dataBytes: 1500, logBytes: 300, totalBytes: 1800 },
  ],
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
    {
      tenantId: null,
      tenantName: null,
      dataBytes: 10,
      logBytes: 5,
      totalBytes: 15,
      databaseCount: 1,
    }, // без клиента
  ],
  databases: [],
};

const detail: SizeExportData = {
  scope: { tenantName: "ООО Ромашка" },
  points: summary.points,
  fromUtc: summary.fromUtc,
  toUtc: summary.toUtc,
  tenants: [],
  databases: [
    { databaseName: "acc_main", dataBytes: 1400, logBytes: 290, totalBytes: 1690 },
    { databaseName: "with;semicolon", dataBytes: 100, logBytes: 10, totalBytes: 110 },
  ],
};

function stripBom(text: string): string {
  return text.charCodeAt(0) === BOM ? text.slice(1) : text;
}

function lines(text: string): string[] {
  return stripBom(text).trimEnd().split("\r\n");
}

describe("toSizeCsv", () => {
  it("prefixes a UTF-8 BOM so RU-Excel detects the encoding", async () => {
    const bytes = new Uint8Array(await toSizeCsv(summary).arrayBuffer());
    expect([bytes[0], bytes[1], bytes[2]]).toEqual([0xef, 0xbb, 0xbf]);
  });

  it("emits the growth section header and raw byte rows", async () => {
    const l = lines(await toSizeCsv(summary).text());
    expect(l[0]).toBe("Момент;Данные (байт);Журнал (байт);Итого (байт)");
    expect(l[1]).toMatch(/^01\.06\.2026 \d{2}:\d{2};1000;200;1200$/);
  });

  it("appends the tenant breakdown section for the summary scope", async () => {
    const l = lines(await toSizeCsv(summary).text());
    // Пустая строка-разделитель + заголовок секции «Клиенты».
    expect(l).toContain("Клиент;Итого (байт);Данные (байт);Журнал (байт);Число баз");
    expect(l).toContain("ООО Ромашка;1800;1500;300;2");
    expect(l).toContain("Без клиента;15;10;5;1");
  });

  it("appends the database section for the detail scope and escapes «;»", async () => {
    const l = lines(await toSizeCsv(detail).text());
    expect(l).toContain("База;Итого (байт);Данные (байт);Журнал (байт)");
    expect(l).toContain("acc_main;1690;1400;290");
    expect(l).toContain('"with;semicolon";110;100;10');
  });
});
