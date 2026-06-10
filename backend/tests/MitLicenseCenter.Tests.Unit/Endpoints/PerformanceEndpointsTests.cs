using FluentAssertions;
using MitLicenseCenter.Application.Clusters;
using MitLicenseCenter.Application.Performance;
using MitLicenseCenter.Domain.Infobases;
using MitLicenseCenter.Domain.Tenants;
using MitLicenseCenter.Infrastructure.Performance.Testing;
using MitLicenseCenter.Web.Endpoints;
using NSubstitute;
using Xunit;

namespace MitLicenseCenter.Tests.Unit.Endpoints;

// MLC-064: эндпоинт GET /performance/host отдаёт live-снимок порта IHostMetricsProbe
// как есть (vertical slice ADR-20). Прогоняем через StubHostMetricsProbe — без WMI.
public sealed class PerformanceEndpointsTests
{
    [Fact]
    public async Task GetHost_returns_probe_snapshot()
    {
        var probe = new StubHostMetricsProbe();

        var result = await PerformanceEndpoints.GetHostAsync(probe, CancellationToken.None);

        result.Value.Should().BeSameAs(probe.Snapshot);
        probe.CaptureCalls.Should().Be(1);
    }

    [Fact]
    public async Task GetHost_passes_through_measuring_flag_and_groups()
    {
        var probe = new StubHostMetricsProbe
        {
            Snapshot = new HostMetricsSnapshot(
                CapturedAtUtc: new DateTime(2026, 6, 8, 9, 0, 0, DateTimeKind.Utc),
                Measuring: true,
                Cpu: new CpuMetrics(0, 0),
                Memory: new MemoryMetrics(1024, 8192, 0),
                Disk: new DiskMetrics(0, 0, 0),
                ProcessGroups: [new ProcessGroupUsage("OneC", 0, 1000, 2)],
                ProcessesInaccessible: 0),
        };

        var result = await PerformanceEndpoints.GetHostAsync(probe, CancellationToken.None);

        var body = result.Value!;
        body.Measuring.Should().BeTrue();
        body.ProcessGroups.Should().ContainSingle().Which.Family.Should().Be("OneC");
    }

    // MLC-064a: число недоступных процессов проходит через эндпоинт, а производный признак
    // «атрибуция неполна» взводится при наличии хотя бы одного непрочитанного процесса.
    [Fact]
    public async Task GetHost_passes_through_inaccessible_count_and_derives_incomplete_flag()
    {
        var probe = new StubHostMetricsProbe
        {
            Snapshot = new HostMetricsSnapshot(
                CapturedAtUtc: new DateTime(2026, 6, 8, 9, 0, 0, DateTimeKind.Utc),
                Measuring: false,
                Cpu: new CpuMetrics(5, 0),
                Memory: new MemoryMetrics(1024, 8192, 0),
                Disk: new DiskMetrics(0, 0, 0),
                ProcessGroups: [new ProcessGroupUsage("Other", 5, 1000, 4)],
                ProcessesInaccessible: 7),
        };

        var result = await PerformanceEndpoints.GetHostAsync(probe, CancellationToken.None);

        var body = result.Value!;
        body.ProcessesInaccessible.Should().Be(7);
        body.AttributionIncomplete.Should().BeTrue();
    }

    [Fact]
    public void AttributionIncomplete_is_false_when_all_processes_readable()
    {
        var snapshot = new HostMetricsSnapshot(
            CapturedAtUtc: new DateTime(2026, 6, 8, 9, 0, 0, DateTimeKind.Utc),
            Measuring: false,
            Cpu: new CpuMetrics(5, 0),
            Memory: new MemoryMetrics(1024, 8192, 0),
            Disk: new DiskMetrics(0, 0, 0),
            ProcessGroups: [],
            ProcessesInaccessible: 0);

        snapshot.AttributionIncomplete.Should().BeFalse();
    }

    // MLC-066: GET /performance/onec-sessions компонует live-снимок из двух Application-портов
    // IClusterClient (сеансы с perf + рабочие процессы) — vertical slice ADR-20, без rac.exe в Web.
    [Fact]
    public async Task GetOneCSessions_composes_session_loads_and_processes()
    {
        var cluster = Substitute.For<IClusterClient>();
        var session = new OneCSessionLoad(
            SessionId: Guid.Parse("02d5184c-65b5-4d8a-ae39-b156b909fcaf"),
            SessionNumber: 1,
            ClusterInfobaseId: Guid.Parse("6256b6f3-dde1-41f9-a6c2-bdfc36bca7aa"),
            AppId: "1CV8C", UserName: "Андрей", Host: "ANDREY-PC",
            Process: Guid.Parse("487281d5-aaaa-bbbb-cccc-ddddeeeeffff"), Connection: null,
            CpuTimeCurrent: 109, DurationCurrent: 422, DurationCurrentDbms: 0,
            MemoryCurrent: -1138560, BlockedByDbms: 0, BlockedByLs: 0,
            LastActiveAtUtc: new DateTime(2026, 6, 8, 20, 21, 45, DateTimeKind.Utc));
        var process = new OneCProcessLoad(
            Process: Guid.Parse("487281d5-aaaa-bbbb-cccc-ddddeeeeffff"),
            Pid: 15876, AvailablePerformance: 416, AvgCallTime: 1.124, MemorySize: 1682404);

        cluster.ListSessionLoadsAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<OneCSessionLoad>>([session]));
        cluster.ListProcessesAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<OneCProcessLoad>>([process]));

        var result = await PerformanceEndpoints.GetOneCSessionsAsync(cluster, CancellationToken.None);

        var body = result.Value!;
        body.Sessions.Should().ContainSingle().Which.MemoryCurrent.Should().Be(-1138560);
        body.Processes.Should().ContainSingle().Which.AvailablePerformance.Should().Be(416);
        body.CapturedAtUtc.Kind.Should().Be(DateTimeKind.Utc);
    }

    [Fact]
    public async Task GetOneCSessions_returns_empty_snapshot_when_cluster_unavailable()
    {
        var cluster = Substitute.For<IClusterClient>();
        cluster.ListSessionLoadsAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<OneCSessionLoad>>([]));
        cluster.ListProcessesAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<OneCProcessLoad>>([]));

        var result = await PerformanceEndpoints.GetOneCSessionsAsync(cluster, CancellationToken.None);

        result.Value!.Sessions.Should().BeEmpty();
        result.Value!.Processes.Should().BeEmpty();
    }

    // MLC-068: GET /performance/sql компонует live-снимок порта ISqlPerformanceProbe (DMV) +
    // атрибуцию по клиенту (database→Infobase→tenant) из своего AppDbContext (vertical slice
    // ADR-20 — Web читает свою БД, к DMV ходит только через порт). Без живого SQL — через стаб.
    [Fact]
    public async Task GetSql_passes_snapshot_and_attributes_databases_to_tenants()
    {
        using var db = TestHelpers.NewInMemoryDb();
        var tenant = new Tenant
        {
            Id = Guid.NewGuid(),
            Name = "Клиент А",
            MaxConcurrentLicenses = 10,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
        };
        db.Tenants.Add(tenant);
        db.Infobases.Add(new Infobase
        {
            Id = Guid.NewGuid(),
            TenantId = tenant.Id,
            Name = "Бухгалтерия",
            ClusterInfobaseId = Guid.NewGuid(),
            DatabaseName = "mitpro",
            Status = InfobaseStatus.Active,
            CreatedAt = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();

        var probe = new StubSqlPerformanceProbe
        {
            Snapshot = new SqlPerformanceSnapshot(
                CapturedAtUtc: new DateTime(2026, 6, 9, 12, 0, 0, DateTimeKind.Utc),
                Status: SqlProbeStatus.Ok,
                Measuring: false,
                ActiveRequests:
                [
                    new SqlActiveRequest(
                        SessionId: 77, BlockingSessionId: null, DatabaseName: "mitpro", IsOneC: true,
                        ProgramName: "1CV83 Server", HostName: "ANDREY-PC", Status: "running",
                        WaitType: null, WaitTimeMs: 0, CpuTimeMs: 1200, ElapsedMs: 1500,
                        LogicalReads: 90000, SqlText: "SELECT T1._IDRRef FROM dbo._Reference172 T1"),
                ],
                DatabaseIo: [new SqlDatabaseIo("master", 10, 5, 3, 1)],
                TopWaits: [new SqlWaitDelta("PAGEIOLATCH_SH", 200, 4)]),
        };

        var result = await PerformanceEndpoints.GetSqlAsync(probe, db, CancellationToken.None);

        var view = result.Value!;
        view.Snapshot.Status.Should().Be(SqlProbeStatus.Ok);
        view.Snapshot.ActiveRequests.Should().ContainSingle().Which.IsOneC.Should().BeTrue();
        view.Snapshot.CapturedAtUtc.Kind.Should().Be(DateTimeKind.Utc);

        // Обе базы из снимка (mitpro из запроса + master из IO) попали в атрибуцию; mitpro → клиент,
        // master → «ничья».
        view.Databases.Should().HaveCount(2);
        view.Databases.Single(d => d.DatabaseName == "mitpro").TenantName.Should().Be("Клиент А");
        var system = view.Databases.Single(d => d.DatabaseName == "master");
        system.TenantId.Should().BeNull();
        system.InfobaseName.Should().BeNull();
    }

    [Fact]
    public async Task GetSql_passes_through_permission_denied_with_empty_attribution()
    {
        using var db = TestHelpers.NewInMemoryDb();
        var probe = new StubSqlPerformanceProbe
        {
            Snapshot = new SqlPerformanceSnapshot(
                CapturedAtUtc: new DateTime(2026, 6, 9, 12, 0, 0, DateTimeKind.Utc),
                Status: SqlProbeStatus.PermissionDenied,
                Measuring: false,
                ActiveRequests: [], DatabaseIo: [], TopWaits: []),
        };

        var result = await PerformanceEndpoints.GetSqlAsync(probe, db, CancellationToken.None);

        var view = result.Value!;
        view.Snapshot.Status.Should().Be(SqlProbeStatus.PermissionDenied);
        view.Databases.Should().BeEmpty();
        probe.CaptureCalls.Should().Be(1);
    }
}
