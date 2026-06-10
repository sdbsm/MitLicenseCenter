using FluentAssertions;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using MitLicenseCenter.Application.Clusters;
using MitLicenseCenter.Domain.Audit;
using MitLicenseCenter.Domain.Infobases;
using MitLicenseCenter.Domain.Tenants;
using MitLicenseCenter.Infrastructure.Persistence;
using MitLicenseCenter.Web.Endpoints;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Xunit;

namespace MitLicenseCenter.Tests.Unit.Endpoints;

// MLC-092 — «нераспределённые» базы кластера: diff снапшота RAS минус заведённые минус
// скрытые; TTL-кэш снапшота (60 c) с обходом по refresh=true; гарды hide/unhide;
// Available:false без утечки сырой ошибки RAS (паттерн discovery, MLC-009); чистка
// игнор-листа при создании Infobase.
public sealed class UnassignedInfobasesEndpointsTests
{
    private static readonly DateTime T0 = new(2026, 6, 11, 10, 0, 0, DateTimeKind.Utc);

    private static IClusterClient ClusterWith(params ClusterInfobase[] infobases)
    {
        var cluster = Substitute.For<IClusterClient>();
        cluster.ListInfobasesAsync(Arg.Any<CancellationToken>())
            .Returns(new ClusterInfobaseDiscoveryResult(infobases, Available: true, Error: null));
        return cluster;
    }

    private static Task<Ok<UnassignedInfobasesResponse>> GetAsync(
        AppDbContext db,
        IClusterClient cluster,
        UnassignedInfobasesCache cache,
        DateTime nowUtc,
        bool? refresh = null) =>
        UnassignedInfobasesEndpoints.GetUnassignedAsync(
            db, cluster, cache, NullLoggerFactory.Instance,
            TestHelpers.FixedClock(nowUtc), refresh, CancellationToken.None);

    // ── Diff ────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Diff_excludes_assigned_and_hidden_infobases()
    {
        using var db = TestHelpers.NewInMemoryDb();
        var free = new ClusterInfobase(Guid.NewGuid(), "Свободная", "служебное описание");
        var assigned = new ClusterInfobase(Guid.NewGuid(), "Заведённая", null);
        var hidden = new ClusterInfobase(Guid.NewGuid(), "Скрытая", null);

        var tenant = new Tenant { Id = Guid.NewGuid(), Name = "Acme", MaxConcurrentLicenses = 10, IsActive = true, CreatedAt = T0 };
        db.Tenants.Add(tenant);
        db.Infobases.Add(new Infobase
        {
            Id = Guid.NewGuid(),
            TenantId = tenant.Id,
            Name = "Заведённая",
            ClusterInfobaseId = assigned.Id,
            DatabaseName = "ib",
            Status = InfobaseStatus.Active,
            CreatedAt = T0,
        });
        db.HiddenClusterInfobases.Add(new HiddenClusterInfobase
        {
            ClusterInfobaseId = hidden.Id,
            Name = "Скрытая",
            HiddenAtUtc = T0,
            HiddenBy = "admin",
        });
        await db.SaveChangesAsync();

        var result = await GetAsync(db, ClusterWith(free, assigned, hidden), new UnassignedInfobasesCache(), T0);

        var body = result.Value!;
        body.Available.Should().BeTrue();
        body.Error.Should().BeNull();
        body.CheckedAtUtc.Should().Be(T0);
        body.Items.Should().ContainSingle();
        body.Items[0].Should().Be(new UnassignedInfobaseItemResponse(free.Id, "Свободная", "служебное описание"));
        body.HiddenItems.Should().ContainSingle();
        body.HiddenItems[0].Should().Be(new HiddenUnassignedInfobaseResponse(hidden.Id, "Скрытая", T0, "admin"));
    }

    // ── Кэш / refresh ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task Second_call_within_ttl_uses_cached_snapshot()
    {
        using var db = TestHelpers.NewInMemoryDb();
        var cluster = ClusterWith(new ClusterInfobase(Guid.NewGuid(), "ИБ", null));
        var cache = new UnassignedInfobasesCache();

        var first = await GetAsync(db, cluster, cache, T0);
        var second = await GetAsync(db, cluster, cache, T0.AddSeconds(30));

        await cluster.Received(1).ListInfobasesAsync(Arg.Any<CancellationToken>());
        second.Value!.CheckedAtUtc.Should().Be(first.Value!.CheckedAtUtc);
    }

    [Fact]
    public async Task Expired_ttl_polls_cluster_again()
    {
        using var db = TestHelpers.NewInMemoryDb();
        var cluster = ClusterWith(new ClusterInfobase(Guid.NewGuid(), "ИБ", null));
        var cache = new UnassignedInfobasesCache();

        await GetAsync(db, cluster, cache, T0);
        var second = await GetAsync(db, cluster, cache, T0.AddSeconds(61));

        await cluster.Received(2).ListInfobasesAsync(Arg.Any<CancellationToken>());
        second.Value!.CheckedAtUtc.Should().Be(T0.AddSeconds(61));
    }

    [Fact]
    public async Task Refresh_true_bypasses_cache()
    {
        using var db = TestHelpers.NewInMemoryDb();
        var cluster = ClusterWith(new ClusterInfobase(Guid.NewGuid(), "ИБ", null));
        var cache = new UnassignedInfobasesCache();

        await GetAsync(db, cluster, cache, T0);
        var second = await GetAsync(db, cluster, cache, T0.AddSeconds(5), refresh: true);

        await cluster.Received(2).ListInfobasesAsync(Arg.Any<CancellationToken>());
        second.Value!.CheckedAtUtc.Should().Be(T0.AddSeconds(5));
    }

    [Fact]
    public async Task Diff_is_not_cached_hide_is_visible_immediately()
    {
        using var db = TestHelpers.NewInMemoryDb();
        var ib = new ClusterInfobase(Guid.NewGuid(), "ИБ", null);
        var cluster = ClusterWith(ib);
        var cache = new UnassignedInfobasesCache();

        (await GetAsync(db, cluster, cache, T0)).Value!.Items.Should().ContainSingle();

        // Скрытие между запросами: снапшот RAS ещё в кэше, но diff живой.
        db.HiddenClusterInfobases.Add(new HiddenClusterInfobase
        {
            ClusterInfobaseId = ib.Id,
            Name = "ИБ",
            HiddenAtUtc = T0,
            HiddenBy = "admin",
        });
        await db.SaveChangesAsync();

        var second = await GetAsync(db, cluster, cache, T0.AddSeconds(10));
        second.Value!.Items.Should().BeEmpty();
        second.Value.HiddenItems.Should().ContainSingle();
        await cluster.Received(1).ListInfobasesAsync(Arg.Any<CancellationToken>());
    }

    // ── Available:false / санитизация (MLC-009) ─────────────────────────────────────

    [Fact]
    public async Task Unavailable_ras_returns_available_false_without_raw_error()
    {
        const string rawStderr = "Соединение с сервером SRV1C-PROD:1545 не установлено (rac.exe)";
        using var db = TestHelpers.NewInMemoryDb();
        db.HiddenClusterInfobases.Add(new HiddenClusterInfobase
        {
            ClusterInfobaseId = Guid.NewGuid(),
            Name = "Скрытая",
            HiddenAtUtc = T0,
            HiddenBy = "admin",
        });
        await db.SaveChangesAsync();

        var cluster = Substitute.For<IClusterClient>();
        cluster.ListInfobasesAsync(Arg.Any<CancellationToken>())
            .Returns(new ClusterInfobaseDiscoveryResult(
                Array.Empty<ClusterInfobase>(), Available: false, Error: rawStderr));

        var result = await GetAsync(db, cluster, new UnassignedInfobasesCache(), T0);

        var body = result.Value!;
        body.Available.Should().BeFalse();
        body.Items.Should().BeEmpty();
        body.Error.Should().NotBeNullOrEmpty();
        body.Error.Should().NotContain("SRV1C-PROD", "имя сервера из stderr rac.exe не должно утекать в UI");
        body.Error.Should().NotContain(rawStderr);
        // Блок «Скрытые» рендерится из БД и не зависит от RAS.
        body.HiddenItems.Should().ContainSingle();
        body.CheckedAtUtc.Should().Be(T0);
    }

    [Fact]
    public async Task Adapter_exception_returns_available_false_without_raw_message()
    {
        const string secret = "rac.exe crashed at C:\\Program Files\\1cv8\\8.3.23.1865\\bin";
        using var db = TestHelpers.NewInMemoryDb();
        var cluster = Substitute.For<IClusterClient>();
        cluster.ListInfobasesAsync(Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException(secret));

        var result = await GetAsync(db, cluster, new UnassignedInfobasesCache(), T0);

        var body = result.Value!;
        body.Available.Should().BeFalse();
        body.Error.Should().NotBeNullOrEmpty();
        body.Error.Should().NotContain(secret);
    }

    [Fact]
    public async Task Cancellation_propagates_instead_of_reporting_as_error()
    {
        using var db = TestHelpers.NewInMemoryDb();
        var cluster = Substitute.For<IClusterClient>();
        cluster.ListInfobasesAsync(Arg.Any<CancellationToken>())
            .ThrowsAsync(new OperationCanceledException());

        var act = async () => await GetAsync(db, cluster, new UnassignedInfobasesCache(), T0);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task Unavailable_snapshot_is_cached_until_ttl()
    {
        using var db = TestHelpers.NewInMemoryDb();
        var cluster = Substitute.For<IClusterClient>();
        cluster.ListInfobasesAsync(Arg.Any<CancellationToken>())
            .Returns(new ClusterInfobaseDiscoveryResult(
                Array.Empty<ClusterInfobase>(), Available: false, Error: "stderr"));
        var cache = new UnassignedInfobasesCache();

        await GetAsync(db, cluster, cache, T0);
        await GetAsync(db, cluster, cache, T0.AddSeconds(30));

        await cluster.Received(1).ListInfobasesAsync(Arg.Any<CancellationToken>());
    }

    // ── Hide: гарды и аудит ─────────────────────────────────────────────────────────

    [Fact]
    public async Task Hide_writes_row_and_audit_14()
    {
        using var db = TestHelpers.NewInMemoryDb();
        var audit = new TestHelpers.CapturingAuditLogger();
        var id = Guid.NewGuid();

        var result = await UnassignedInfobasesEndpoints.HideAsync(
            id, new HideUnassignedInfobaseRequest("Служебная"), db, audit,
            TestHelpers.NewHttpContext("admin"), TestHelpers.FixedClock(T0), CancellationToken.None);

        result.Result.Should().BeOfType<NoContent>();
        var row = db.HiddenClusterInfobases.Single();
        row.ClusterInfobaseId.Should().Be(id);
        row.Name.Should().Be("Служебная");
        row.HiddenAtUtc.Should().Be(T0);
        row.HiddenBy.Should().Be("admin");

        var entry = audit.Entries.Should().ContainSingle().Subject;
        entry.Action.Should().Be(AuditActionType.UnassignedInfobaseHidden);
        ((int)entry.Action).Should().Be(14, "int-значения аудита заморожены");
        entry.TenantId.Should().BeNull("server-scope: база ещё не принадлежит клиенту");
        entry.Description.Should().Contain("Служебная").And.Contain("admin");
    }

    [Fact]
    public async Task Hide_of_assigned_infobase_returns_409()
    {
        using var db = TestHelpers.NewInMemoryDb();
        var tenant = new Tenant { Id = Guid.NewGuid(), Name = "Acme", MaxConcurrentLicenses = 10, IsActive = true, CreatedAt = T0 };
        var clusterId = Guid.NewGuid();
        db.Tenants.Add(tenant);
        db.Infobases.Add(new Infobase
        {
            Id = Guid.NewGuid(),
            TenantId = tenant.Id,
            Name = "Бухгалтерия",
            ClusterInfobaseId = clusterId,
            DatabaseName = "ib",
            Status = InfobaseStatus.Active,
            CreatedAt = T0,
        });
        await db.SaveChangesAsync();
        var audit = new TestHelpers.CapturingAuditLogger();

        var result = await UnassignedInfobasesEndpoints.HideAsync(
            clusterId, new HideUnassignedInfobaseRequest("Бухгалтерия"), db, audit,
            TestHelpers.NewHttpContext(), TestHelpers.FixedClock(T0), CancellationToken.None);

        var conflict = result.Result.Should().BeOfType<Conflict<ProblemDetails>>().Subject;
        conflict.Value!.Extensions["code"].Should().Be(ProblemCodes.UnassignedAlreadyAssigned);
        db.HiddenClusterInfobases.Should().BeEmpty();
        audit.Entries.Should().BeEmpty();
    }

    [Fact]
    public async Task Double_hide_returns_409()
    {
        using var db = TestHelpers.NewInMemoryDb();
        var audit = new TestHelpers.CapturingAuditLogger();
        var id = Guid.NewGuid();

        var first = await UnassignedInfobasesEndpoints.HideAsync(
            id, new HideUnassignedInfobaseRequest("Служебная"), db, audit,
            TestHelpers.NewHttpContext(), TestHelpers.FixedClock(T0), CancellationToken.None);
        first.Result.Should().BeOfType<NoContent>();

        var second = await UnassignedInfobasesEndpoints.HideAsync(
            id, new HideUnassignedInfobaseRequest("Служебная"), db, audit,
            TestHelpers.NewHttpContext(), TestHelpers.FixedClock(T0), CancellationToken.None);

        var conflict = second.Result.Should().BeOfType<Conflict<ProblemDetails>>().Subject;
        conflict.Value!.Extensions["code"].Should().Be(ProblemCodes.UnassignedAlreadyHidden);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("  ")]
    public async Task Hide_with_empty_name_returns_400(string? name)
    {
        using var db = TestHelpers.NewInMemoryDb();

        var result = await UnassignedInfobasesEndpoints.HideAsync(
            Guid.NewGuid(), new HideUnassignedInfobaseRequest(name), db,
            new TestHelpers.CapturingAuditLogger(),
            TestHelpers.NewHttpContext(), TestHelpers.FixedClock(T0), CancellationToken.None);

        result.Result.Should().BeOfType<ValidationProblem>();
    }

    [Fact]
    public async Task Hide_with_name_over_200_chars_returns_400_not_500()
    {
        // Гоча minimal API: DataAnnotations в runtime не прогоняются, nvarchar(200)
        // уронил бы вставку — длину проверяет сам обработчик.
        using var db = TestHelpers.NewInMemoryDb();

        var result = await UnassignedInfobasesEndpoints.HideAsync(
            Guid.NewGuid(), new HideUnassignedInfobaseRequest(new string('б', 201)), db,
            new TestHelpers.CapturingAuditLogger(),
            TestHelpers.NewHttpContext(), TestHelpers.FixedClock(T0), CancellationToken.None);

        result.Result.Should().BeOfType<ValidationProblem>();
    }

    // ── Unhide ──────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Unhide_removes_row_and_writes_audit_15()
    {
        using var db = TestHelpers.NewInMemoryDb();
        var id = Guid.NewGuid();
        db.HiddenClusterInfobases.Add(new HiddenClusterInfobase
        {
            ClusterInfobaseId = id,
            Name = "Служебная",
            HiddenAtUtc = T0,
            HiddenBy = "admin",
        });
        await db.SaveChangesAsync();
        var audit = new TestHelpers.CapturingAuditLogger();

        var result = await UnassignedInfobasesEndpoints.UnhideAsync(
            id, db, audit, TestHelpers.NewHttpContext("operator"), CancellationToken.None);

        result.Result.Should().BeOfType<NoContent>();
        db.HiddenClusterInfobases.Should().BeEmpty();

        var entry = audit.Entries.Should().ContainSingle().Subject;
        entry.Action.Should().Be(AuditActionType.UnassignedInfobaseUnhidden);
        ((int)entry.Action).Should().Be(15, "int-значения аудита заморожены");
        entry.TenantId.Should().BeNull();
        entry.Description.Should().Contain("Служебная").And.Contain("operator");
    }

    [Fact]
    public async Task Unhide_of_unknown_id_returns_404()
    {
        using var db = TestHelpers.NewInMemoryDb();
        var audit = new TestHelpers.CapturingAuditLogger();

        var result = await UnassignedInfobasesEndpoints.UnhideAsync(
            Guid.NewGuid(), db, audit, TestHelpers.NewHttpContext(), CancellationToken.None);

        result.Result.Should().BeOfType<NotFound>();
        audit.Entries.Should().BeEmpty();
    }

    // ── Чистка игнор-листа при создании Infobase ────────────────────────────────────

    [Fact]
    public async Task Create_infobase_removes_cluster_id_from_ignore_list()
    {
        using var db = TestHelpers.NewInMemoryDb();
        var tenant = new Tenant { Id = Guid.NewGuid(), Name = "Acme", MaxConcurrentLicenses = 10, IsActive = true, CreatedAt = T0 };
        db.Tenants.Add(tenant);
        var clusterId = Guid.NewGuid();
        db.HiddenClusterInfobases.Add(new HiddenClusterInfobase
        {
            ClusterInfobaseId = clusterId,
            Name = "Бывшая служебная",
            HiddenAtUtc = T0,
            HiddenBy = "admin",
        });
        await db.SaveChangesAsync();

        var request = new CreateInfobaseRequest(
            TenantId: tenant.Id,
            Name: "Бухгалтерия",
            ClusterInfobaseId: clusterId,
            DatabaseName: "ib",
            Status: InfobaseStatus.Active,
            Publication: new CreatePublicationRequest(
                SiteName: "Default Web Site",
                VirtualPath: "/ib",
                PlatformVersion: "8.3.23.1865",
                PhysicalPathOverride: null));

        var result = await InfobasesEndpoints.CreateAsync(
            request, db, new TestHelpers.CapturingAuditLogger(),
            TestHelpers.NewHttpContext(), TestHelpers.FixedClock(T0), CancellationToken.None);

        result.Result.Should().BeOfType<Created<InfobaseDetailResponse>>();
        db.HiddenClusterInfobases.Should().BeEmpty("заведённая база перестала быть «нераспределённой»");
    }
}
