using Microsoft.Data.SqlClient;

namespace MitLicenseCenter.Infrastructure.Persistence;

// Ранний bootstrap-шаг (ADR-18): создаёт целевую БД панели, если её ещё нет, ДО миграций,
// Hangfire-регистрации и сидинга. Первопричина: EF `MigrateAsync` сам НЕ создаёт несуществующую
// БД под `EnableRetryOnFailure` (DependencyInjection.cs) — ошибка 4060 «cannot open database»
// трактуется retry-стратегией как транзиентная и ретраится, затем падает (fail-fast → краш, БД
// не создаётся; воспроизведено на стенде). Поэтому создание БД делаем одним сырым statement к
// `master` (retry ни при чём), а не через EF. Существующую БД НЕ трогаем (`IF DB_ID IS NULL`).
//
// Сырой Microsoft.Data.SqlClient здесь допустим — это слой доступа к БД (Persistence), не адаптер
// к 1С/IIS (ADR-20); Web зовёт статический метод так же, как сидеры. Требует прав CREATE DATABASE
// (sysadmin/dbcreator) — уже требуется каноном для discovery/бэкапов.
public static class DatabaseBootstrapper
{
    // Создаёт БД из InitialCatalog строки подключения, если её ещё нет. No-op, если имя БД пусто
    // (например, InMemory-провайдер в тестах сюда не попадает — гейтит вызывающий код).
    public static async Task EnsureDatabaseCreatedAsync(string connectionString, CancellationToken ct = default)
    {
        var databaseName = GetDatabaseName(connectionString);
        if (string.IsNullOrEmpty(databaseName))
        {
            return;
        }

        var masterConnectionString = ToMasterConnectionString(connectionString);

        await using var connection = new SqlConnection(masterConnectionString);
        await connection.OpenAsync(ct).ConfigureAwait(false);

        // Имя БД проверяем параметром в DB_ID(@name) (защита от инъекции); в CREATE DATABASE [...]
        // подставляем экранированное имя (`]`→`]]`) — DDL не принимает имя объекта параметром.
        // Имя берётся из конфига оператора; параметр + экранирование — defense-in-depth.
        await using var command = connection.CreateCommand();
        command.CommandText =
            "IF DB_ID(@name) IS NULL EXEC('CREATE DATABASE [" + EscapeIdentifier(databaseName) + "]');";
        command.Parameters.AddWithValue("@name", databaseName);

        await command.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
    }

    // Имя целевой БД из строки подключения (InitialCatalog / "Database"). Пусто → вызывающий no-op.
    internal static string GetDatabaseName(string connectionString)
    {
        var builder = new SqlConnectionStringBuilder(connectionString);
        return builder.InitialCatalog;
    }

    // Строка подключения к master с теми же кредами/Encrypt/TrustServerCertificate, что у Default —
    // меняется только каталог (InitialCatalog = "master"). Так создание БД идёт под той же учёткой,
    // что и приложение, без отдельных кредов (тот же приём, что у SqlDatabaseDiscovery).
    internal static string ToMasterConnectionString(string connectionString)
    {
        var builder = new SqlConnectionStringBuilder(connectionString)
        {
            InitialCatalog = "master",
        };
        return builder.ConnectionString;
    }

    private static string EscapeIdentifier(string name) => name.Replace("]", "]]", StringComparison.Ordinal);
}
