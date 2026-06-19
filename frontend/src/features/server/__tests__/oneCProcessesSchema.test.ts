import { describe, it, expect } from "vitest";
import { oneCProcessesSchema } from "../useOneCProcesses";

// BE опускает null-поля (гоча api-omits-null-fields): pid / availablePerformance /
// avgCallTime / memorySize приходят либо со значением, либо ключ отсутствует — НИКОГДА null.
// Схема использует omittable() → парсится в обоих случаях, нормализуя в null.
describe("oneCProcessesSchema (MLC-219, omit-null граница)", () => {
  it("парсит процесс с ОПУЩЕННЫМИ perf-полями (ключей нет)", () => {
    const wire = {
      processes: [{ process: "487281d5-aaaa-bbbb-cccc-ddddeeeeffff" }],
    };

    const parsed = oneCProcessesSchema.parse(wire);

    const p = parsed.processes[0];
    expect(p.process).toBe("487281d5-aaaa-bbbb-cccc-ddddeeeeffff");
    expect(p.pid).toBeNull();
    expect(p.availablePerformance).toBeNull();
    expect(p.avgCallTime).toBeNull();
    expect(p.memorySize).toBeNull();
  });

  it("парсит процесс с присутствующими perf-полями", () => {
    const wire = {
      processes: [
        {
          process: "487281d5-aaaa-bbbb-cccc-ddddeeeeffff",
          pid: 15876,
          availablePerformance: 416,
          avgCallTime: 1.124,
          memorySize: 1682404,
        },
      ],
    };

    const parsed = oneCProcessesSchema.parse(wire);

    const p = parsed.processes[0];
    expect(p.pid).toBe(15876);
    expect(p.availablePerformance).toBe(416);
    expect(p.avgCallTime).toBe(1.124);
    expect(p.memorySize).toBe(1682404);
  });

  it("degraded-ветка: пустой список процессов (rac недоступен)", () => {
    const parsed = oneCProcessesSchema.parse({ processes: [] });
    expect(parsed.processes).toEqual([]);
  });
});
