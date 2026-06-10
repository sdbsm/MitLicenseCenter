using FluentAssertions;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.Logging.Abstractions;
using MitLicenseCenter.Application.Discovery;
using MitLicenseCenter.Application.Publishing;
using MitLicenseCenter.Application.Settings;
using MitLicenseCenter.Domain.Settings;
using MitLicenseCenter.Web.Endpoints;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Xunit;

namespace MitLicenseCenter.Tests.Unit.Endpoints;

// MLC-009: при инфраструктурном исключении discovery возвращает Available:false и
// санитизированный русский Error — БЕЗ сырого ex.Message (имена серверов, пути,
// SQL/COM-детали). Отмена запроса (OperationCanceledException) не выдаётся за
// «ошибку discovery», а пробрасывается.
public sealed class DiscoveryEndpointsTests
{
    // Сервер берётся из настройки Sql.Server (single-host, MLC-087); снапшот мокаем.
    private static ISettingsSnapshot SettingsWithSqlServer(string? server)
    {
        var settings = Substitute.For<ISettingsSnapshot>();
        settings.GetString(SettingKey.SqlServer).Returns(server);
        return settings;
    }

    [Fact]
    public async Task GetDatabases_on_exception_returns_unavailable_without_raw_message()
    {
        const string secret = "Login failed for user 'sa' on server SQLPROD01\\MSSQL.";
        var discovery = Substitute.For<ISqlDatabaseDiscovery>();
        discovery.ListDatabasesAsync(Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException(secret));

        var result = await DiscoveryEndpoints.GetDatabasesAsync(
            discovery,
            SettingsWithSqlServer("SQLPROD01"),
            NullLoggerFactory.Instance,
            CancellationToken.None);

        var body = result.Value!;
        body.Available.Should().BeFalse();
        body.Items.Should().BeEmpty();
        body.Error.Should().NotBeNullOrEmpty();
        body.Error.Should().NotContain(secret);
        body.Error.Should().NotContain("SQLPROD01", "имя сервера не должно утекать в UI");
        body.Error.Should().Contain("Не удалось получить список баз данных");
    }

    [Fact]
    public async Task GetDatabases_unset_setting_returns_unavailable_without_calling_discovery()
    {
        var discovery = Substitute.For<ISqlDatabaseDiscovery>();

        var result = await DiscoveryEndpoints.GetDatabasesAsync(
            discovery,
            SettingsWithSqlServer("  "),
            NullLoggerFactory.Instance,
            CancellationToken.None);

        result.Value!.Available.Should().BeFalse();
        result.Value.Error.Should().Be("Сервер СУБД не задан. Укажите его в разделе «Параметры».");
        await discovery.DidNotReceiveWithAnyArgs().ListDatabasesAsync(default);
    }

    [Fact]
    public async Task GetDatabases_propagates_cancellation_instead_of_reporting_as_error()
    {
        var discovery = Substitute.For<ISqlDatabaseDiscovery>();
        discovery.ListDatabasesAsync(Arg.Any<CancellationToken>())
            .ThrowsAsync(new OperationCanceledException());

        var act = async () => await DiscoveryEndpoints.GetDatabasesAsync(
            discovery,
            SettingsWithSqlServer("SQLPROD01"),
            NullLoggerFactory.Instance,
            new CancellationToken(canceled: true));

        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task GetIisSites_on_exception_returns_unavailable_without_raw_message()
    {
        const string secret = "COMException 0x80070005: C:\\Windows\\System32\\inetsrv\\config\\applicationHost.config";
        var iis = Substitute.For<IIisPublishingService>();
        iis.ListSitesAsync(Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException(secret));

        var result = await DiscoveryEndpoints.GetIisSitesAsync(
            iis,
            NullLoggerFactory.Instance,
            CancellationToken.None);

        var body = result.Value!;
        body.Available.Should().BeFalse();
        body.Items.Should().BeEmpty();
        body.Error.Should().NotBeNullOrEmpty();
        body.Error.Should().NotContain(secret);
        body.Error.Should().NotContain("applicationHost.config");
        body.Error.Should().Contain("Не удалось получить список сайтов IIS");
    }

    [Fact]
    public async Task GetIisSites_propagates_cancellation_instead_of_reporting_as_error()
    {
        var iis = Substitute.For<IIisPublishingService>();
        iis.ListSitesAsync(Arg.Any<CancellationToken>())
            .ThrowsAsync(new OperationCanceledException());

        var act = async () => await DiscoveryEndpoints.GetIisSitesAsync(
            iis,
            NullLoggerFactory.Instance,
            new CancellationToken(canceled: true));

        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public void GetSqlInstances_on_exception_returns_unavailable_without_raw_message()
    {
        const string secret = "Access denied to HKLM\\SOFTWARE\\Microsoft\\Microsoft SQL Server for SQLPROD01.";
        var discovery = Substitute.For<ISqlInstanceDiscovery>();
        discovery.FindLocalInstances().Throws(new UnauthorizedAccessException(secret));

        var result = DiscoveryEndpoints.GetSqlInstances(discovery, NullLoggerFactory.Instance);

        var body = result.Value!;
        body.Available.Should().BeFalse();
        body.Items.Should().BeEmpty();
        body.Error.Should().NotBeNullOrEmpty();
        body.Error.Should().NotContain(secret);
        body.Error.Should().NotContain("HKLM", "детали реестра не должны утекать в UI");
        body.Error.Should().Contain("Не удалось получить список инстансов SQL Server");
    }

    [Fact]
    public void GetSqlInstances_returns_instances_when_available()
    {
        var discovery = Substitute.For<ISqlInstanceDiscovery>();
        string[] instances = ["localhost", "localhost\\SQLEXPRESS"];
        discovery.FindLocalInstances().Returns(instances);

        var result = DiscoveryEndpoints.GetSqlInstances(discovery, NullLoggerFactory.Instance);

        var body = result.Value!;
        body.Available.Should().BeTrue();
        body.Error.Should().BeNull();
        body.Items.Should().Equal("localhost", "localhost\\SQLEXPRESS");
    }
}
