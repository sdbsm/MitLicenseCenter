// @vitest-environment node
// Чистый сериалайзер без DOM: node-окружение даёт Blob с .text() (jsdom-Blob не имеет).
import { describe, expect, it } from "vitest";
import type { RecordingSample } from "../../types";
import { recordingToCsv } from "../recordingCsv";

const BOM = 0xfeff;

const sample: RecordingSample = {
  sampleUtc: "2026-06-09T10:00:15Z",
  measuring: false,
  cpuPercent: 42.55,
  cpuQueueLength: 1.2,
  memoryAvailableMBytes: 4096,
  memoryTotalMBytes: 16384,
  memoryPagesPerSec: 12.3,
  diskAvgReadSecPerOp: 0.002,
  diskAvgWriteSecPerOp: 0.005,
  diskQueueLength: 0.5,
  processesInaccessible: 0,
  processGroups: [],
  oneC: null,
  sql: null,
};

function stripBom(text: string): string {
  return text.charCodeAt(0) === BOM ? text.slice(1) : text;
}

function dataLines(text: string): string[] {
  return stripBom(text).trimEnd().split("\r\n");
}

describe("recordingToCsv", () => {
  it("prefixes a UTF-8 BOM so RU-Excel detects the encoding", async () => {
    const bytes = new Uint8Array(await recordingToCsv([sample]).arrayBuffer());
    expect([bytes[0], bytes[1], bytes[2]]).toEqual([0xef, 0xbb, 0xbf]);
  });

  it("uses «;» separator and the expected header", async () => {
    const lines = dataLines(await recordingToCsv([sample]).text());
    expect(lines[0]).toBe(
      "Время;ЦП %;Очередь ЦП;Память свободно МБ;Память всего МБ;Обмен стр/с;" +
        "Диск чтение мс;Диск запись мс;Очередь диска;Недоступно процессов"
    );
  });

  it("rounds with a decimal comma and converts disk latency to ms", async () => {
    const lines = dataLines(await recordingToCsv([sample]).text());
    // 42.55 → 42,6 ; диск чтение 0.002с → 2 мс ; запись 0.005с → 5 мс
    expect(lines[1]).toMatch(/;42,6;1,2;4096;16384;12,3;2;5;0,5;0$/);
  });

  it("emits one data row per sample", async () => {
    const lines = dataLines(await recordingToCsv([sample, sample]).text());
    expect(lines).toHaveLength(3);
  });

  it("handles an empty series (header only)", async () => {
    const lines = dataLines(await recordingToCsv([]).text());
    expect(lines).toHaveLength(1);
  });
});
