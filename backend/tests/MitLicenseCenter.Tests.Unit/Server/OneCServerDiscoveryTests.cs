using FluentAssertions;
using MitLicenseCenter.Infrastructure.Ras;
using MitLicenseCenter.Infrastructure.Server;
using Xunit;

namespace MitLicenseCenter.Tests.Unit.Server;

// Обнаружение служб сервера 1С (ragent) по ImagePath через фейковые
// IServiceRegistryReader/IServiceStateReader (тот же приём, что ScRasServiceManagerTests):
// тест подаёт заранее заготовленный список служб, реальный реестр/ServiceController не
// дёргаются. Несколько установленных версий платформы → несколько служб.
public sealed class OneCServerDiscoveryTests
{
    private const string Ragent83 = @"C:\Program Files\1cv8\8.3.23.1865\bin\ragent.exe";
    private const string Ragent85 = @"C:\Program Files\1cv8\8.5.1.1302\bin\ragent.exe";

    [Fact]
    public void Discover_filters_ragent_services_out_of_full_list()
    {
        var registry = new FakeRegistry()
            .Add("LanmanServer", @"C:\Windows\System32\svchost.exe -k netsvcs")
            .Add("W3SVC", @"C:\Windows\System32\svchost.exe -k iissvcs")
            .Add("1C:Enterprise 8.3.23.1865 Server Agent", $"\"{Ragent83}\" -srvc -agent -port 1540");
        var state = new FakeState().SetRunning("1C:Enterprise 8.3.23.1865 Server Agent", true);

        var result = new OneCServerDiscovery(registry, state).Discover();

        result.Should().ContainSingle();
        result[0].ServiceName.Should().Be("1C:Enterprise 8.3.23.1865 Server Agent");
        result[0].Running.Should().BeTrue();
        result[0].PlatformVersion.Should().Be("8.3.23.1865");
    }

    [Fact]
    public void Discover_returns_one_entry_per_installed_version()
    {
        var registry = new FakeRegistry()
            .Add("1C Server 8.3", $"\"{Ragent83}\" -srvc -agent -port 1540")
            .Add("1C Server 8.5", $"\"{Ragent85}\" -srvc -agent -port 1560");
        var state = new FakeState()
            .SetRunning("1C Server 8.3", true)
            .SetRunning("1C Server 8.5", false);

        var result = new OneCServerDiscovery(registry, state).Discover();

        result.Should().HaveCount(2);
        result.Should().ContainSingle(s => s.PlatformVersion == "8.3.23.1865" && s.Running);
        result.Should().ContainSingle(s => s.PlatformVersion == "8.5.1.1302" && !s.Running);
    }

    [Fact]
    public void Discover_treats_missing_state_as_stopped()
    {
        // Служба найдена в реестре, но ServiceController не вернул состояние (исчезла между
        // чтением реестра и проверкой) → Running=false.
        var registry = new FakeRegistry()
            .Add("1C Server", $"\"{Ragent83}\" -srvc -agent -port 1540");
        var state = new FakeState(); // состояния нет

        var result = new OneCServerDiscovery(registry, state).Discover();

        result.Should().ContainSingle();
        result[0].Running.Should().BeFalse();
    }

    [Fact]
    public void Discover_returns_empty_when_no_ragent_service()
    {
        var registry = new FakeRegistry()
            .Add("W3SVC", @"C:\Windows\System32\svchost.exe -k iissvcs")
            .Add("MSSQLSERVER", @"C:\Program Files\...\sqlservr.exe -sMSSQLSERVER");

        var result = new OneCServerDiscovery(registry, new FakeState()).Discover();

        result.Should().BeEmpty();
    }

    [Fact]
    public void Discover_null_version_when_path_nonstandard()
    {
        // ragent.exe есть, но без сегмента \<версия>\bin\ — версия не парсится (best-effort).
        var registry = new FakeRegistry()
            .Add("1C Server", @"D:\custom\ragent.exe -srvc -agent -port 1540");
        var state = new FakeState().SetRunning("1C Server", true);

        var result = new OneCServerDiscovery(registry, state).Discover();

        result.Should().ContainSingle();
        result[0].PlatformVersion.Should().BeNull();
        result[0].Running.Should().BeTrue();
    }

    // ── fakes (адаптированы из ScRasServiceManagerTests) ────────────────────────────────

    private sealed class FakeRegistry : IServiceRegistryReader
    {
        private readonly List<RegisteredService> _services = new();

        public FakeRegistry Add(string name, string imagePath)
        {
            _services.Add(new RegisteredService(name, imagePath));
            return this;
        }

        public IReadOnlyList<RegisteredService> ReadServices() => _services;
    }

    private sealed class FakeState : IServiceStateReader
    {
        private readonly Dictionary<string, ServiceState> _byName = new(StringComparer.OrdinalIgnoreCase);

        public FakeState SetRunning(string name, bool running)
        {
            _byName[name] = new ServiceState(running, name);
            return this;
        }

        public ServiceState? ReadState(string serviceName)
            => _byName.TryGetValue(serviceName, out var s) ? s : null;
    }
}
