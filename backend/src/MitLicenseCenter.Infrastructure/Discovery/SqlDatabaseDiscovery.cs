using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using MitLicenseCenter.Application.Discovery;
using MitLicenseCenter.Application.Settings;
using MitLicenseCenter.Domain.Settings;

namespace MitLicenseCenter.Infrastructure.Discovery;

// Перечисляет пользовательские БД на SQL-инстансе панели через sys.databases.
// Сервер — настройка Sql.Server (единственный источник, single-host MLC-087).
// Параметры аутентификации (Trusted_Connection / TrustServerCertificate / Encrypt)
// наследуются из ConnectionStrings:Default — меняются только сервер и каталог
// (master). Так discovery работает под той же учёткой, что и приложение, и не
// требует отдельных кредов. Возвращает ТОЛЬКО имена баз (не содержимое).
internal sealed class SqlDatabaseDiscovery : ISqlDatabaseDiscovery
{
    // Короткий таймаут: discovery интерактивен, оператор не должен ждать.
    private const int ConnectTimeoutSeconds = 5;
    private const int CommandTimeoutSeconds = 5;

    private readonly string _baseConnectionString;
    private readonly ISettingsSnapshot _settings;

    public SqlDatabaseDiscovery(IConfiguration configuration, ISettingsSnapshot settings)
    {
        _baseConnectionString = configuration.GetConnectionString("Default")
            ?? throw new InvalidOperationException("ConnectionStrings:Default не задан.");
        _settings = settings;
    }

    public async Task<IReadOnlyList<string>> ListDatabasesAsync(CancellationToken ct)
    {
        var server = _settings.GetString(SettingKey.SqlServer);
        // Эндпоинт гейтит пустую настройку до вызова; здесь — defense-in-depth.
        ArgumentException.ThrowIfNullOrWhiteSpace(server);

        var builder = new SqlConnectionStringBuilder(_baseConnectionString)
        {
            DataSource = server,
            InitialCatalog = "master",
            ConnectTimeout = ConnectTimeoutSeconds,
        };

        var databases = new List<string>();

        await using var connection = new SqlConnection(builder.ConnectionString);
        await connection.OpenAsync(ct).ConfigureAwait(false);

        // database_id > 4 исключает системные master/tempdb/model/msdb.
        await using var command = connection.CreateCommand();
        command.CommandText =
            "SELECT name FROM sys.databases WHERE database_id > 4 ORDER BY name;";
        command.CommandTimeout = CommandTimeoutSeconds;

        await using var reader = await command.ExecuteReaderAsync(ct).ConfigureAwait(false);
        while (await reader.ReadAsync(ct).ConfigureAwait(false))
        {
            databases.Add(reader.GetString(0));
        }

        return databases;
    }
}
