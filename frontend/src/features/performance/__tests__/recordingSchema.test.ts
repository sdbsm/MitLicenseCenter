import { describe, expect, it } from "vitest";
import { recordingDetailSchema, recordingListSchema, recordingSummarySchema } from "../types";

/**
 * Регрессия (урок [[api-omits-null-fields]], применён превентивно как в MLC-069): бэкенд
 * сериализует `null`-поля **пропуском ключа** (`JsonIgnoreCondition.WhenWritingNull`). У активной
 * записи `stoppedAtUtc`/`stopReason` не приходят как `null`, а отсутствуют; у сэмпла без 1С/SQL
 * `oneC`/`sql` отсутствуют. Схема обязана это принимать (`omittable()` = `.nullish()` +
 * нормализация в `null`), иначе Zod-граница отвергнет весь ответ и секция упадёт в ошибку.
 */
describe("recordingSummarySchema", () => {
  it("принимает активную запись с пропущенными stoppedAtUtc/stopReason", () => {
    const raw = {
      id: "11111111-1111-1111-1111-111111111111",
      startedAtUtc: "2026-06-09T10:00:00Z",
      status: "Active",
      startedBy: "operator",
      sampleCount: 3,
    };
    const parsed = recordingSummarySchema.parse(raw);
    expect(parsed.stoppedAtUtc).toBeNull();
    expect(parsed.stopReason).toBeNull();
    expect(parsed.status).toBe("Active");
  });

  it("принимает завершённую запись с stopReason", () => {
    const parsed = recordingSummarySchema.parse({
      id: "22222222-2222-2222-2222-222222222222",
      startedAtUtc: "2026-06-09T10:00:00Z",
      stoppedAtUtc: "2026-06-09T10:30:00Z",
      status: "Stopped",
      startedBy: "operator",
      stopReason: "Manual",
      sampleCount: 120,
    });
    expect(parsed.stoppedAtUtc).toBe("2026-06-09T10:30:00Z");
    expect(parsed.stopReason).toBe("Manual");
  });

  it("валидирует список", () => {
    const parsed = recordingListSchema.parse([
      {
        id: "33333333-3333-3333-3333-333333333333",
        startedAtUtc: "2026-06-09T10:00:00Z",
        status: "Interrupted",
        startedBy: "system",
        sampleCount: 0,
      },
    ]);
    expect(parsed).toHaveLength(1);
    expect(parsed[0].status).toBe("Interrupted");
  });
});

describe("recordingDetailSchema", () => {
  it("принимает сэмпл с пропущенными oneC/sql и нормализует их в null", () => {
    const parsed = recordingDetailSchema.parse({
      recording: {
        id: "44444444-4444-4444-4444-444444444444",
        startedAtUtc: "2026-06-09T10:00:00Z",
        stoppedAtUtc: "2026-06-09T10:15:00Z",
        status: "Stopped",
        startedBy: "operator",
        stopReason: "TimeLimit",
        sampleCount: 1,
      },
      samples: [
        {
          // сэмпл ровно как отдаёт бэкенд: oneC/sql отсутствуют (источники не настроены)
          sampleUtc: "2026-06-09T10:00:15Z",
          measuring: false,
          cpuPercent: 42.5,
          cpuQueueLength: 1,
          memoryAvailableMBytes: 4096,
          memoryTotalMBytes: 16384,
          memoryPagesPerSec: 12,
          diskAvgReadSecPerOp: 0.002,
          diskAvgWriteSecPerOp: 0.004,
          diskQueueLength: 0.5,
          processesInaccessible: 0,
          processGroups: [{ family: "OneC", cpuPercent: 30, ramBytes: 1024, processCount: 2 }],
        },
      ],
    });
    expect(parsed.samples[0].oneC).toBeNull();
    expect(parsed.samples[0].sql).toBeNull();
    expect(parsed.samples[0].processGroups[0].family).toBe("OneC");
  });
});
