import { describe, expect, it } from "vitest";
import { dashboardSummarySchema } from "../types";

/**
 * FE-19 (MLC-120). Wire-fixture для сводки дашборда — сырой ответ «как с провода»
 * прогоняется через Zod-границу (раньше `useDashboardSummary` слепо кастил `api<T>()`).
 *
 * Урок [[api-omits-null-fields]] (MLC-067/071): бэкенд сериализует `null`-поля
 * **пропуском ключа** (`JsonIgnoreCondition.WhenWritingNull`). У RAS-health до первого
 * ping'а `lastCheckedAtUtc`/`lastErrorMessage` не приходят как `null`, а отсутствуют —
 * схема обязана это принимать (`omittable()`) и нормализовать в `null`.
 */
describe("dashboardSummarySchema", () => {
  it("принимает ответ с ОПУЩЕННЫМИ nullable-полями ras (omit-null) → поля === null", () => {
    const raw = {
      tenantsTotal: 2,
      tenantsActive: 2,
      infobasesTotal: 3,
      sessionsActiveTotal: 5,
      licensesConsumedTotal: 5,
      licensesAvailableTotal: 95,
      topTenantsByConsumption: [],
      ras: {
        // lastCheckedAtUtc и lastErrorMessage ОТСУТСТВУЮТ (первый ping ещё не прошёл).
        healthy: false,
        consecutiveFailures: 0,
      },
    };

    const parsed = dashboardSummarySchema.parse(raw);

    expect(parsed.ras.lastCheckedAtUtc).toBeNull();
    expect(parsed.ras.lastErrorMessage).toBeNull();
    expect(parsed.ras.healthy).toBe(false);
    expect(parsed.tenantsActive).toBe(2);
  });

  it("принимает заполненный ответ (RAS здоров, проверка прошла)", () => {
    const parsed = dashboardSummarySchema.parse({
      tenantsTotal: 4,
      tenantsActive: 3,
      infobasesTotal: 7,
      sessionsActiveTotal: 12,
      licensesConsumedTotal: 12,
      licensesAvailableTotal: 88,
      topTenantsByConsumption: [
        {
          tenantId: "11111111-1111-1111-1111-111111111111",
          tenantName: "Acme",
          consumed: 8,
          limit: 10,
          percent: 80,
        },
      ],
      ras: {
        healthy: true,
        lastCheckedAtUtc: "2026-06-13T08:00:00Z",
        lastErrorMessage: null,
        consecutiveFailures: 0,
      },
    });

    expect(parsed.ras.lastCheckedAtUtc).toBe("2026-06-13T08:00:00Z");
    expect(parsed.topTenantsByConsumption).toHaveLength(1);
    expect(parsed.topTenantsByConsumption[0].percent).toBe(80);
  });

  it("принимает ответ с lastErrorMessage при нездоровом RAS", () => {
    const parsed = dashboardSummarySchema.parse({
      tenantsTotal: 1,
      tenantsActive: 1,
      infobasesTotal: 1,
      sessionsActiveTotal: 0,
      licensesConsumedTotal: 0,
      licensesAvailableTotal: 10,
      topTenantsByConsumption: [],
      ras: {
        healthy: false,
        lastCheckedAtUtc: "2026-06-13T08:05:00Z",
        lastErrorMessage: "RAS недоступен: connection refused.",
        consecutiveFailures: 3,
      },
    });

    expect(parsed.ras.lastErrorMessage).toContain("connection refused");
    expect(parsed.ras.consecutiveFailures).toBe(3);
  });

  it("незнакомое доп.поле будущего бэкенда НЕ роняет парс (толерантность, как backups)", () => {
    const parsed = dashboardSummarySchema.parse({
      tenantsTotal: 0,
      tenantsActive: 0,
      infobasesTotal: 0,
      sessionsActiveTotal: 0,
      licensesConsumedTotal: 0,
      licensesAvailableTotal: 0,
      topTenantsByConsumption: [],
      ras: { healthy: true, consecutiveFailures: 0, futureRasField: "ignored" },
      futureTopLevelField: 42,
    });

    expect(parsed.tenantsTotal).toBe(0);
    expect(parsed.ras.healthy).toBe(true);
  });

  it("отсутствие required-числа отвергается (граница строга к контракту)", () => {
    expect(() =>
      dashboardSummarySchema.parse({
        // tenantsTotal отсутствует
        tenantsActive: 1,
        infobasesTotal: 1,
        sessionsActiveTotal: 0,
        licensesConsumedTotal: 0,
        licensesAvailableTotal: 10,
        topTenantsByConsumption: [],
        ras: { healthy: true, consecutiveFailures: 0 },
      })
    ).toThrow();
  });
});
