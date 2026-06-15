import type { SessionSnapshotEntry } from "@/features/sessions/types";
import { useSessionsSnapshot } from "@/features/sessions/useSessionsSnapshot";

/**
 * Агрегация текущего потребления лицензий по клиенту на фронтенде (MLC-122 / R6).
 *
 * Намеренное небольшое дублирование канонического backend-метода
 * `LicenseConsumption.CountByTenant`: на фронтенде это live-оверлей для
 * визуального акцента, а не контракт/parity-правило. Данные берутся из того
 * же снапшота сеансов, что использует дашборд, — значения совпадают.
 *
 * Логика: consumed[tenantId] = число items где licenseStatus === "Consuming"
 * (ADR-48: факт rac --licenses), сгруппированных по tenantId (идентично
 * `LicenseConsumption.CountByTenant`).
 */

/** Чистая функция агрегации — отдельно для тестируемости. */
export function buildConsumedByTenant(items: SessionSnapshotEntry[]): Map<string, number> {
  const result = new Map<string, number>();
  for (const item of items) {
    if (item.licenseStatus === "Consuming") {
      result.set(item.tenantId, (result.get(item.tenantId) ?? 0) + 1);
    }
  }
  return result;
}

/**
 * Хук: возвращает Map<tenantId, consumed> из live-снапшота сеансов.
 * Обновляется каждые 5 с (интервал useSessionsSnapshot).
 */
export function useTenantConsumption(): {
  consumedByTenant: Map<string, number>;
  isLoading: boolean;
} {
  const { data, isLoading } = useSessionsSnapshot();

  const consumedByTenant = data ? buildConsumedByTenant(data.items) : new Map<string, number>();

  return { consumedByTenant, isLoading };
}
