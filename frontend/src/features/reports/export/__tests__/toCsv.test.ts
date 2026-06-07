// @vitest-environment node
// Чистый сериалайзер без DOM: node-окружение даёт Blob с .text() (jsdom-Blob не имеет).
import { describe, expect, it } from "vitest";
import type { LicenseUsageSeriesResponse } from "../../types";
import { toCsv } from "../toCsv";

const BOM = 0xfeff;

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

async function read(blob: Blob): Promise<string> {
  return await blob.text();
}

/** Снять ведущий BOM (U+FEFF), если есть — чтобы разбирать таблицу без него. */
function stripBom(text: string): string {
  return text.charCodeAt(0) === BOM ? text.slice(1) : text;
}

function dataLines(text: string): string[] {
  return stripBom(text).trimEnd().split("\r\n");
}

describe("toCsv", () => {
  it("prefixes a UTF-8 BOM so RU-Excel detects the encoding", async () => {
    // Проверяем сырые байты: TextDecoder в blob.text() срезает ведущий BOM при
    // декодировании, поэтому смотрим первые три байта (EF BB BF), а не строку.
    const bytes = new Uint8Array(await toCsv(sample).arrayBuffer());
    expect([bytes[0], bytes[1], bytes[2]]).toEqual([0xef, 0xbb, 0xbf]);
  });

  it("uses «;» separator and the expected header", async () => {
    const lines = dataLines(await read(toCsv(sample)));
    expect(lines[0]).toBe("Начало бакета;Среднее;Пик;Лимит");
  });

  it("rounds the average to tenths with a decimal comma (max/limit verbatim)", async () => {
    const lines = dataLines(await read(toCsv(sample)));
    // 3.46 → 3.5 → «3,5». Час форматируется в локальной TZ, поэтому проверяем
    // дату (день не плывёт у 12:00Z) и числовой хвост строки, не точное время.
    expect(lines[1]).toMatch(/^01\.06\.2026 \d{2}:\d{2};3,5;5;10$/);
  });

  it("emits one data row per bucket", async () => {
    const lines = dataLines(await read(toCsv(sample)));
    expect(lines).toHaveLength(1 + sample.buckets.length);
  });

  it("handles an empty series (header only)", async () => {
    const lines = dataLines(await read(toCsv({ ...sample, buckets: [] })));
    expect(lines).toEqual(["Начало бакета;Среднее;Пик;Лимит"]);
  });
});
