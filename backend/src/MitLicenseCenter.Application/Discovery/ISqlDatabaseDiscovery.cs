namespace MitLicenseCenter.Application.Discovery;

// Discovery имён баз данных на указанном MSSQL-сервере. Используется формой
// инфобазы вместо ручного ввода имени БД. Источник истины — sys.databases.
public interface ISqlDatabaseDiscovery
{
    // Может бросить исключение при недоступности сервера/ошибке подключения —
    // вызывающий (эндпоинт) ловит и помечает результат как недоступный.
    Task<IReadOnlyList<string>> ListDatabasesAsync(string server, CancellationToken ct);
}
