import type { SqlActiveRequest, SqlDatabaseAttribution } from "./types";

/**
 * Чистая логика вкладки SQL-активности (MLC-069): сшивка строк DMV с атрибуцией база→клиент,
 * агрегация «кто грузит SQL по базе», ранжирование активных запросов и выявление цепочек
 * блокировок. Без React — покрыто unit-тестами. Форматирование длительностей переиспользуется
 * из `onecLoad` (ms-значения), здесь — только целочисленный формат счётчиков.
 */

// Индекс атрибуции по имени базы (регистронезависимо — SQL и Infobase.DatabaseName
// сопоставляются без учёта регистра, как в бэкенд-резолвере SqlAttributionResolver).
export function buildAttributionMap(
  databases: readonly SqlDatabaseAttribution[]
): Map<string, SqlDatabaseAttribution> {
  const map = new Map<string, SqlDatabaseAttribution>();
  for (const d of databases) {
    const name = d.databaseName.trim();
    if (name) map.set(name.toLowerCase(), d);
  }
  return map;
}

export function attributionFor(
  databaseName: string | null,
  map: Map<string, SqlDatabaseAttribution>
): SqlDatabaseAttribution | null {
  if (!databaseName) return null;
  return map.get(databaseName.trim().toLowerCase()) ?? null;
}

// Строка сводки «нагрузка по базам»: суммарное ЦП-время и число активных запросов по базе
// + клиент-атрибуция. cpuTimeMs = null, когда ни у одного запроса базы нет ЦП-метрики.
export interface DatabaseLoadRow {
  databaseName: string;
  attribution: SqlDatabaseAttribution | null;
  requestCount: number;
  oneCRequestCount: number;
  cpuTimeMs: number | null;
}

// Джойн активных запросов с атрибуцией: группировка по базе, сумма ЦП, счётчики. Запросы без
// имени базы (databaseName=null) в сводку не попадают (нечего атрибутировать), но остаются в
// таблице активных запросов. Сортировка — по суммарному ЦП вниз (null тонут).
export function aggregateByDatabase(
  requests: readonly SqlActiveRequest[],
  map: Map<string, SqlDatabaseAttribution>
): DatabaseLoadRow[] {
  const byDb = new Map<string, DatabaseLoadRow>();
  for (const r of requests) {
    const name = r.databaseName?.trim();
    if (!name) continue;
    const key = name.toLowerCase();
    let row = byDb.get(key);
    if (!row) {
      row = {
        databaseName: name,
        attribution: attributionFor(name, map),
        requestCount: 0,
        oneCRequestCount: 0,
        cpuTimeMs: null,
      };
      byDb.set(key, row);
    }
    row.requestCount += 1;
    if (r.isOneC) row.oneCRequestCount += 1;
    if (r.cpuTimeMs !== null) row.cpuTimeMs = (row.cpuTimeMs ?? 0) + r.cpuTimeMs;
  }
  return [...byDb.values()].sort((a, b) => (b.cpuTimeMs ?? -1) - (a.cpuTimeMs ?? -1));
}

// Ранг «кто грузит» для таблицы активных запросов: по ЦП-времени, при равенстве — по
// длительности. Отсутствующие (`null`) метрики тонут вниз. Не мутирует вход.
export function sortRequestsByCpu(requests: readonly SqlActiveRequest[]): SqlActiveRequest[] {
  return [...requests].sort((a, b) => {
    const cpu = (b.cpuTimeMs ?? -1) - (a.cpuTimeMs ?? -1);
    if (cpu !== 0) return cpu;
    return (b.elapsedMs ?? -1) - (a.elapsedMs ?? -1);
  });
}

// Запрос ждёт другой сеанс (звено цепочки блокировок).
export function isBlocked(r: SqlActiveRequest): boolean {
  return r.blockingSessionId !== null;
}

// Сеансы, которые блокируют другие (вершина/середина цепочки): их session-id фигурирует как
// blocking-session-id у заблокированных. Подсветка «блокирует» делает цепочку видимой: A ждёт
// сеанс B (B помечен «блокирует»), даже если сам запрос B уже не в выборке активных.
export function collectBlockerIds(requests: readonly SqlActiveRequest[]): Set<number> {
  const ids = new Set<number>();
  for (const r of requests) {
    if (r.blockingSessionId !== null) ids.add(r.blockingSessionId);
  }
  return ids;
}

// Целое с разрядными разбивками (обычный пробел, детерминированно — без locale-зависимого
// разделителя, чтобы тесты были стабильны); null → «—». Отрицательный знак — типографский «−».
export function formatInt(n: number | null): string {
  if (n === null) return "—";
  const sign = n < 0 ? "−" : "";
  return (
    sign +
    Math.abs(n)
      .toString()
      .replace(/\B(?=(\d{3})+(?!\d))/g, " ")
  );
}
