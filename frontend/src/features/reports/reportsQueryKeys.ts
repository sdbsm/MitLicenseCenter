/**
 * Query-ключи для фичи «Отчёты» (MLC-122).
 *
 * Вынесены в отдельный файл, чтобы избежать циклического импорта:
 * useTenants → reportsQueryKey → useLicenseUsage → (потребители отчётов) → useTenants.
 * useLicenseUsage реэкспортирует reportsQueryKey отсюда — публичный API не меняется.
 */
export const reportsQueryKey = ["reports", "license-usage"] as const;

// MLC-185f: ключ отчёта «Размер баз» (отдельный кэш-неймспейс от лицензий).
export const databaseSizeQueryKey = ["reports", "database-size"] as const;
