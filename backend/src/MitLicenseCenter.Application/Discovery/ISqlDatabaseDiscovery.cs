namespace MitLicenseCenter.Application.Discovery;

// Discovery имён баз данных на SQL-инстансе панели. Используется формой инфобазы
// вместо ручного ввода имени БД. Источник истины — sys.databases. Сервер берётся
// из настройки Sql.Server (single-host, MLC-087) — параметра сервера в контракте нет.
public interface ISqlDatabaseDiscovery
{
    // Может бросить исключение при недоступности сервера/ошибке подключения —
    // вызывающий (эндпоинт) ловит и помечает результат как недоступный. Бросает
    // ArgumentException, если настройка Sql.Server пуста (эндпоинт гейтит до вызова).
    Task<IReadOnlyList<string>> ListDatabasesAsync(CancellationToken ct);
}
