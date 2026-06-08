namespace MitLicenseCenter.Application.Performance;

// Ответ эндпоинта GET /performance/sql: live-снимок DMV (от пробы) + атрибуция по клиенту
// (от панели). Проба знает только имена баз из DMV; сшивку database→Infobase→tenant делает
// vertical slice эндпоинта по AppDbContext (ADR-20 — Web может читать свой DbContext напрямую,
// но к DMV ходит только через порт). Фронт (MLC-069) джойнит строки DMV с Databases по имени
// базы, чтобы показать «по базе/клиенту».
public sealed record SqlPerformanceView(
    SqlPerformanceSnapshot Snapshot,
    IReadOnlyList<SqlDatabaseAttribution> Databases);

// Привязка базы данных SQL к клиенту панели. TenantId/TenantName/InfobaseName = null, когда базе
// из DMV не соответствует ни одна зарегистрированная инфобаза (системные master/tempdb, БД самой
// панели, незарегистрированная база) — фронт всё равно показывает строку с именем базы и клиентом
// «—». Гранулярность — база (SQL→сеанс→юзер невозможна, ADR-26).
public sealed record SqlDatabaseAttribution(
    string DatabaseName,
    Guid? TenantId,
    string? TenantName,
    string? InfobaseName);

// Нейтральная проекция инфобазы для атрибуции (DatabaseName→клиент). Эндпоинт наполняет её из
// AppDbContext (Infobase ⨝ Tenant), не протаскивая доменные сущности в чистый резолвер.
public sealed record InfobaseDatabaseRef(
    string DatabaseName,
    Guid TenantId,
    string TenantName,
    string InfobaseName);

// Чистая сшивка имён баз из DMV с инфобазами панели (тестируется без БД). Регистронезависимо
// (SQL и Infobase.DatabaseName сопоставляются без учёта регистра). Каждое различимое имя из DMV
// даёт ровно одну запись; при отсутствии инфобазы — с null-клиентом (база видна, но «ничья»).
public static class SqlAttributionResolver
{
    public static IReadOnlyList<SqlDatabaseAttribution> Resolve(
        IEnumerable<string> databaseNames,
        IReadOnlyList<InfobaseDatabaseRef> infobases)
    {
        // Первая инфобаза на имя базы (имена баз на одном сервере уникальны; при дубле берём
        // первую детерминированно — по имени инфобазы).
        var byDatabase = new Dictionary<string, InfobaseDatabaseRef>(StringComparer.OrdinalIgnoreCase);
        foreach (var ib in infobases.OrderBy(i => i.InfobaseName, StringComparer.Ordinal))
        {
            if (!string.IsNullOrWhiteSpace(ib.DatabaseName))
            {
                byDatabase.TryAdd(ib.DatabaseName.Trim(), ib);
            }
        }

        var result = new List<SqlDatabaseAttribution>();
        foreach (var name in databaseNames.Where(n => !string.IsNullOrWhiteSpace(n)).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            result.Add(byDatabase.TryGetValue(name.Trim(), out var ib)
                ? new SqlDatabaseAttribution(name, ib.TenantId, ib.TenantName, ib.InfobaseName)
                : new SqlDatabaseAttribution(name, null, null, null));
        }

        return result;
    }
}
