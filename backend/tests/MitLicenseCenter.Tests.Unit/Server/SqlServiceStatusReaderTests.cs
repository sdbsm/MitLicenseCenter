using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using MitLicenseCenter.Application.Discovery;
using MitLicenseCenter.Infrastructure.Ras;
using MitLicenseCenter.Infrastructure.Server;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Xunit;

namespace MitLicenseCenter.Tests.Unit.Server;

// Статус локальной службы SQL для агрегатора (MLC-213): нормальный кейс + never-throws на
// сбое реестра/инстанса. Фейковые IServiceRegistryReader/IServiceStateReader; имя инстанса —
// мок ISqlInstanceDiscovery.
public sealed class SqlServiceStatusReaderTests
{
    private const string SqlImagePath =
        @"C:\Program Files\Microsoft SQL Server\MSSQL16.MSSQLSERVER\MSSQL\Binn\sqlservr.exe -sMSSQLSERVER";

    [Fact]
    public void Read_returns_running_when_sqlservr_found()
    {
        var registry = new FakeRegistry()
            .Add("W3SVC", @"C:\Windows\System32\svchost.exe -k iissvcs")
            .Add("MSSQLSERVER", SqlImagePath);
        var state = new FakeState().SetRunning("MSSQLSERVER", true);
        var instances = Substitute.For<ISqlInstanceDiscovery>();
        instances.FindLocalInstances().Returns((IReadOnlyList<string>)["localhost"]);

        var summary = NewReader(registry, state, instances).Read();

        summary.Available.Should().BeTrue();
        summary.Running.Should().BeTrue();
        summary.ServiceName.Should().Be("MSSQLSERVER");
        summary.Instance.Should().Be("localhost");
        summary.Error.Should().BeNull();
    }

    [Fact]
    public void Read_available_but_no_service_when_sql_not_installed()
    {
        var registry = new FakeRegistry()
            .Add("W3SVC", @"C:\Windows\System32\svchost.exe -k iissvcs");
        var instances = Substitute.For<ISqlInstanceDiscovery>();
        instances.FindLocalInstances().Returns(Array.Empty<string>());

        var summary = NewReader(registry, new FakeState(), instances).Read();

        // Служба не найдена — это доступный статус «нет службы», не ошибка адаптера.
        summary.Available.Should().BeTrue();
        summary.ServiceName.Should().BeNull();
        summary.Running.Should().BeFalse();
        summary.Error.Should().BeNull();
    }

    [Fact]
    public void Read_never_throws_when_registry_fails()
    {
        var registry = new FakeRegistry().FailWith(new InvalidOperationException("реестр недоступен"));
        var instances = Substitute.For<ISqlInstanceDiscovery>();
        instances.FindLocalInstances().Returns(Array.Empty<string>());

        var summary = NewReader(registry, new FakeState(), instances).Read();

        summary.Available.Should().BeFalse();
        summary.Error.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void Read_tolerates_instance_discovery_failure()
    {
        // Сбой discovery имени инстанса не должен валить статус самой службы (best-effort).
        var registry = new FakeRegistry().Add("MSSQLSERVER", SqlImagePath);
        var state = new FakeState().SetRunning("MSSQLSERVER", true);
        var instances = Substitute.For<ISqlInstanceDiscovery>();
        instances.FindLocalInstances().Throws(new InvalidOperationException("реестр инстансов недоступен"));

        var summary = NewReader(registry, state, instances).Read();

        summary.Available.Should().BeTrue();
        summary.Running.Should().BeTrue();
        summary.Instance.Should().BeNull();
    }

    private static SqlServiceStatusReader NewReader(
        IServiceRegistryReader registry,
        IServiceStateReader state,
        ISqlInstanceDiscovery instances)
        => new(registry, state, instances, NullLogger<SqlServiceStatusReader>.Instance);

    private sealed class FakeRegistry : IServiceRegistryReader
    {
        private readonly List<RegisteredService> _services = new();
        private Exception? _failure;

        public FakeRegistry Add(string name, string imagePath)
        {
            _services.Add(new RegisteredService(name, imagePath));
            return this;
        }

        public FakeRegistry FailWith(Exception ex)
        {
            _failure = ex;
            return this;
        }

        public IReadOnlyList<RegisteredService> ReadServices()
            => _failure is not null ? throw _failure : _services;
    }

    private sealed class FakeState : IServiceStateReader
    {
        private readonly Dictionary<string, ServiceState> _byName = new(StringComparer.OrdinalIgnoreCase);

        public FakeState SetRunning(string name, bool running)
        {
            _byName[name] = new ServiceState(running, !running, name);
            return this;
        }

        public ServiceState? ReadState(string serviceName)
            => _byName.TryGetValue(serviceName, out var s) ? s : null;
    }
}
