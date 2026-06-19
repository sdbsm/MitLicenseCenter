import { describe, it, expect } from "vitest";
import { autoRestartScheduleSchema } from "../useAutoRestartSchedule";

// MLC-218: BE опускает null-поля (гоча api-omits-null-fields): lastRunUtc приходит либо со
// значением, либо ключ отсутствует — НИКОГДА null. Схема использует omittable() → парсится
// в обоих случаях, нормализуя в null.
describe("autoRestartScheduleSchema (MLC-218, omit-null граница)", () => {
  it("парсит ответ с ОПУЩЕННЫМ lastRunUtc (ключа нет — ещё не запускалась)", () => {
    const wire = {
      enabled: false,
      time: "04:00",
      targetServices: ["ragent_83"],
    };

    const parsed = autoRestartScheduleSchema.parse(wire);

    expect(parsed.enabled).toBe(false);
    expect(parsed.time).toBe("04:00");
    expect(parsed.lastRunUtc).toBeNull();
    expect(parsed.targetServices).toEqual(["ragent_83"]);
  });

  it("парсит ответ с присутствующим lastRunUtc и включённым расписанием", () => {
    const wire = {
      enabled: true,
      time: "02:30",
      lastRunUtc: "2026-06-19T01:00:00.0000000Z",
      targetServices: ["ragent_83", "ragent_85"],
    };

    const parsed = autoRestartScheduleSchema.parse(wire);

    expect(parsed.enabled).toBe(true);
    expect(parsed.time).toBe("02:30");
    expect(parsed.lastRunUtc).toBe("2026-06-19T01:00:00.0000000Z");
    expect(parsed.targetServices).toHaveLength(2);
  });

  it("пустой targetServices (сервер 1С не запущен) — валидный ответ", () => {
    const parsed = autoRestartScheduleSchema.parse({
      enabled: true,
      time: "04:00",
      targetServices: [],
    });
    expect(parsed.targetServices).toEqual([]);
  });
});
