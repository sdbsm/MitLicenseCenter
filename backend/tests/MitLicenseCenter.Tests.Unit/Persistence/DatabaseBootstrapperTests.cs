using FluentAssertions;
using Microsoft.Data.SqlClient;
using MitLicenseCenter.Infrastructure.Persistence;
using Xunit;

namespace MitLicenseCenter.Tests.Unit.Persistence;

// MLC-106 (ADR-18): чистые под-хелперы ранного bootstrap БД. Сам CREATE DATABASE —
// SQL-Server-специфичен (ручная приёмка на стенде: дроп БД → старт службы → БД создаётся).
public sealed class DatabaseBootstrapperTests
{
    [Fact]
    public void GetDatabaseName_extracts_initial_catalog()
    {
        var cs = "Server=.;Database=MitLicenseCenter;Trusted_Connection=True;";

        DatabaseBootstrapper.GetDatabaseName(cs).Should().Be("MitLicenseCenter");
    }

    [Fact]
    public void GetDatabaseName_returns_empty_when_initial_catalog_absent()
    {
        var cs = "Server=.;Trusted_Connection=True;";

        DatabaseBootstrapper.GetDatabaseName(cs).Should().BeEmpty();
    }

    [Fact]
    public void ToMasterConnectionString_replaces_database_with_master()
    {
        var cs = "Server=.;Database=MitLicenseCenter;Trusted_Connection=True;";

        var master = DatabaseBootstrapper.ToMasterConnectionString(cs);

        new SqlConnectionStringBuilder(master).InitialCatalog.Should().Be("master");
    }

    [Fact]
    public void ToMasterConnectionString_preserves_sql_credentials()
    {
        var cs = "Server=sql01;Database=MitLicenseCenter;User Id=mlc;Password=p@ss;";

        var builder = new SqlConnectionStringBuilder(DatabaseBootstrapper.ToMasterConnectionString(cs));

        builder.InitialCatalog.Should().Be("master");
        builder.DataSource.Should().Be("sql01");
        builder.UserID.Should().Be("mlc");
        builder.Password.Should().Be("p@ss");
    }

    [Fact]
    public void ToMasterConnectionString_preserves_encrypt_and_trust_server_certificate()
    {
        var cs = "Server=.;Database=MitLicenseCenter;Trusted_Connection=True;"
            + "Encrypt=True;TrustServerCertificate=True;";

        var builder = new SqlConnectionStringBuilder(DatabaseBootstrapper.ToMasterConnectionString(cs));

        builder.InitialCatalog.Should().Be("master");
        builder.Encrypt.Should().Be(SqlConnectionEncryptOption.Mandatory);
        builder.TrustServerCertificate.Should().BeTrue();
        builder.IntegratedSecurity.Should().BeTrue();
    }

    [Fact]
    public async Task EnsureDatabaseCreatedAsync_is_noop_when_initial_catalog_empty()
    {
        // Имя БД пусто → ранний return без попытки подключения к серверу.
        var cs = "Server=.;Trusted_Connection=True;";

        await DatabaseBootstrapper.EnsureDatabaseCreatedAsync(cs);
    }
}
