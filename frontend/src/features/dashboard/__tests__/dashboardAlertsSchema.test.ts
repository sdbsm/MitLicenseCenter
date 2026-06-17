import { describe, expect, it } from "vitest";
import { dashboardAlertsSchema } from "../types";

/**
 * MLC-186a. Wire-fixture для серверного агрегата сигналов «Требует внимания»
 * (/dashboard/alerts). Толерантная Zod-граница, как у summary.
 *
 * Урок [[api-omits-null-fields]] (MLC-067/071): бэкенд опускает null-поля
 * (`JsonIgnoreCondition.WhenWritingNull`). Для Viewer `clusterDrift` отсутствует вовсе
 * (дрейф — Admin-only); в degraded отсутствуют `freeBytes` и счётчики дрейфа. Схема
 * обязана это принимать (`omittable()`) и нормализовать в `null`.
 */
describe("dashboardAlertsSchema", () => {
  it("принимает Viewer-ответ с ОПУЩЕННЫМ clusterDrift → null (дрейф Admin-only)", () => {
    const raw = {
      quotaWarning: 1,
      quotaDanger: 0,
      // clusterDrift ОТСУТСТВУЕТ (вызывающий не Admin).
      backupDisk: {
        configured: true,
        // freeBytes ОТСУТСТВУЕТ (sysadmin/SQL недоступны — «не знаем»).
        safetyMarginBytes: 2147483648,
        low: false,
      },
    };

    const parsed = dashboardAlertsSchema.parse(raw);

    expect(parsed.clusterDrift).toBeNull();
    expect(parsed.backupDisk.freeBytes).toBeNull();
    expect(parsed.quotaWarning).toBe(1);
    expect(parsed.backupDisk.configured).toBe(true);
    expect(parsed.backupDisk.low).toBe(false);
  });

  it("принимает заполненный Admin-ответ (дрейф доступен, диск настроен)", () => {
    const parsed = dashboardAlertsSchema.parse({
      quotaWarning: 2,
      quotaDanger: 1,
      clusterDrift: {
        available: true,
        unassignedBases: 3,
        basesNotInCluster: 1,
      },
      backupDisk: {
        configured: true,
        freeBytes: 5368709120,
        safetyMarginBytes: 2147483648,
        low: false,
      },
    });

    expect(parsed.quotaDanger).toBe(1);
    expect(parsed.clusterDrift?.available).toBe(true);
    expect(parsed.clusterDrift?.unassignedBases).toBe(3);
    expect(parsed.clusterDrift?.basesNotInCluster).toBe(1);
    expect(parsed.backupDisk.freeBytes).toBe(5368709120);
  });

  it("принимает clusterDrift при недоступном RAS (Available:false → счётчики опущены/null)", () => {
    const parsed = dashboardAlertsSchema.parse({
      quotaWarning: 0,
      quotaDanger: 0,
      clusterDrift: {
        available: false,
        // unassignedBases / basesNotInCluster ОТСУТСТВУЮТ — RAS недоступен, не «ложный ноль».
      },
      backupDisk: { configured: false, safetyMarginBytes: 0, low: false },
    });

    expect(parsed.clusterDrift?.available).toBe(false);
    expect(parsed.clusterDrift?.unassignedBases).toBeNull();
    expect(parsed.clusterDrift?.basesNotInCluster).toBeNull();
    expect(parsed.backupDisk.configured).toBe(false);
  });

  it("disk low: free < margin → low=true", () => {
    const parsed = dashboardAlertsSchema.parse({
      quotaWarning: 0,
      quotaDanger: 0,
      backupDisk: {
        configured: true,
        freeBytes: 1073741824,
        safetyMarginBytes: 2147483648,
        low: true,
      },
    });

    expect(parsed.backupDisk.low).toBe(true);
  });

  it("незнакомое доп.поле будущего бэкенда НЕ роняет парс (толерантность)", () => {
    const parsed = dashboardAlertsSchema.parse({
      quotaWarning: 0,
      quotaDanger: 0,
      backupDisk: { configured: false, safetyMarginBytes: 0, low: false, futureField: 1 },
      futureTopLevelField: 42,
    });

    expect(parsed.quotaWarning).toBe(0);
  });

  it("отсутствие required-числа отвергается (граница строга к контракту)", () => {
    expect(() =>
      dashboardAlertsSchema.parse({
        // quotaWarning отсутствует
        quotaDanger: 0,
        backupDisk: { configured: false, safetyMarginBytes: 0, low: false },
      })
    ).toThrow();
  });
});
