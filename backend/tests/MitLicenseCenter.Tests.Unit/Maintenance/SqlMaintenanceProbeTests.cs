using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using MitLicenseCenter.Application.Maintenance;
using MitLicenseCenter.Application.Settings;
using MitLicenseCenter.Domain.Settings;
using MitLicenseCenter.Infrastructure.Maintenance;
using Xunit;

namespace MitLicenseCenter.Tests.Unit.Maintenance;

// MLC-216: SqlMaintenanceProbe «never throws» — деградация статусом без 500. Сам T-SQL к
// msdb.dbo.backupset (HAS_PERMS_BY_NAME / агрегация по типам) — integration-only (как
// SqlPerformanceProbe/SqlBackupAdapter); юнитами покрываем degraded-ветки без обращения к SQL.
// ISettingsSnapshot — РУЧНОЙ фейк (NSubstitute не проксирует internal-адаптеры Infrastructure —
// нет InternalsVisibleTo("DynamicProxyGenAssembly2")).
public sealed class SqlMaintenanceProbeTests
{
    [Fact]
    public async Task Returns_unavailable_when_sql_server_setting_missing()
    {
        // Нет настройки Sql.Server → Unavailable (опросить нечего), databases пуст, без броска.
        var probe = NewProbe(connectionString: @"Server=x;Database=master;", sqlServer: null);

        var snapshot = await probe.GetBackupFreshnessAsync(CancellationToken.None);

        snapshot.Status.Should().Be(MaintenanceProbeStatus.Unavailable);
        snapshot.Databases.Should().BeEmpty();
    }

    [Fact]
    public async Task Returns_unavailable_when_connection_string_missing()
    {
        // Нет ConnectionStrings:Default → Unavailable, databases пуст, без броска.
        var probe = NewProbe(connectionString: null, sqlServer: "localhost");

        var snapshot = await probe.GetBackupFreshnessAsync(CancellationToken.None);

        snapshot.Status.Should().Be(MaintenanceProbeStatus.Unavailable);
        snapshot.Databases.Should().BeEmpty();
    }

    [Fact]
    public async Task Returns_unavailable_when_sql_unreachable()
    {
        // Сервер задан, но недоступен → SqlException ловится → Unavailable (never-throws).
        // Короткий connect-timeout, чтобы тест не висел.
        var probe = NewProbe(
            connectionString: "Server=255.255.255.255;Database=master;Connect Timeout=1;",
            sqlServer: "255.255.255.255");

        var snapshot = await probe.GetBackupFreshnessAsync(CancellationToken.None);

        snapshot.Status.Should().Be(MaintenanceProbeStatus.Unavailable);
        snapshot.Databases.Should().BeEmpty();
    }

    // ── планы обслуживания (MLC-217) — degraded-ветки без SQL ────────────────────────────

    [Fact]
    public async Task Plans_unavailable_when_sql_server_setting_missing()
    {
        var probe = NewProbe(connectionString: @"Server=x;Database=master;", sqlServer: null);

        var snapshot = await probe.GetMaintenancePlansAsync(CancellationToken.None);

        snapshot.Status.Should().Be(MaintenancePlansStatus.Unavailable);
        snapshot.Plans.Should().BeEmpty();
    }

    [Fact]
    public async Task Plans_unavailable_when_connection_string_missing()
    {
        var probe = NewProbe(connectionString: null, sqlServer: "localhost");

        var snapshot = await probe.GetMaintenancePlansAsync(CancellationToken.None);

        snapshot.Status.Should().Be(MaintenancePlansStatus.Unavailable);
        snapshot.Plans.Should().BeEmpty();
    }

    [Fact]
    public async Task Plans_unavailable_when_sql_unreachable()
    {
        var probe = NewProbe(
            connectionString: "Server=255.255.255.255;Database=master;Connect Timeout=1;",
            sqlServer: "255.255.255.255");

        var snapshot = await probe.GetMaintenancePlansAsync(CancellationToken.None);

        snapshot.Status.Should().Be(MaintenancePlansStatus.Unavailable);
        snapshot.Plans.Should().BeEmpty();
    }

    private static SqlMaintenanceProbe NewProbe(string? connectionString, string? sqlServer)
    {
        var settings = new Dictionary<string, string?>();
        if (connectionString is not null)
        {
            settings["ConnectionStrings:Default"] = connectionString;
        }

        var configuration = new ConfigurationBuilder().AddInMemoryCollection(settings).Build();
        return new SqlMaintenanceProbe(
            configuration,
            new FakeSettingsSnapshot(SettingKey.SqlServer, sqlServer),
            TimeProvider.System,
            NullLogger<SqlMaintenanceProbe>.Instance);
    }

    // Ручной фейк ISettingsSnapshot (без NSubstitute): отдаёт одно строковое значение по ключу.
    private sealed class FakeSettingsSnapshot(string key, string? value) : ISettingsSnapshot
    {
        public string? GetString(string requestedKey) =>
            string.Equals(requestedKey, key, StringComparison.Ordinal) ? value : null;

        public int? GetInt(string requestedKey) => null;

        public void Invalidate()
        {
        }
    }
}
