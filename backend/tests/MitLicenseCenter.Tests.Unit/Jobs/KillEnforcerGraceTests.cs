using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using MitLicenseCenter.Application.Clusters;
using MitLicenseCenter.Application.Sessions;
using MitLicenseCenter.Application.Settings;
using MitLicenseCenter.Domain.Settings;
using MitLicenseCenter.Domain.Tenants;
using MitLicenseCenter.Infrastructure.Jobs;
using MitLicenseCenter.Tests.Unit.Diagnostics;
using MitLicenseCenter.Tests.Unit.Endpoints;
using NSubstitute;
using Xunit;

namespace MitLicenseCenter.Tests.Unit.Jobs;

public sealed class KillEnforcerGraceTests
{
    private static readonly DateTime ClockNow = new(2026, 5, 20, 10, 0, 0, DateTimeKind.Utc);

    // Сеанс моложе grace (15с) не завершается: даём 1С проставить user-name,
    // а не убиваем в окне «подключился, но ещё не вошёл». Newest-first означает,
    // что молодой кандидат идёт первым → break → у тенанта вообще не убиваем.
    [Fact]
    public async Task Does_not_kill_when_newest_candidate_is_within_grace()
    {
        var (cluster, audit) = await RunAsync(
            newestStartedAtUtc: ClockNow.AddSeconds(-5),
            newestUserName: "user-newest");

        await cluster.DidNotReceive().KillSessionAsync(Arg.Any<SessionDescriptor>(), Arg.Any<CancellationToken>(), Arg.Any<string?>());
        audit.Entries.Should().BeEmpty();
    }

    // Тот же сценарий, но самый свежий сеанс уже перешагнул grace → завершается.
    [Fact]
    public async Task Kills_when_newest_candidate_is_past_grace()
    {
        var (cluster, audit) = await RunAsync(
            newestStartedAtUtc: ClockNow.AddSeconds(-20),
            newestUserName: "user-newest");

        await cluster.Received(1).KillSessionAsync(Arg.Any<SessionDescriptor>(), Arg.Any<CancellationToken>(), Arg.Any<string?>());
        audit.Entries.Should().ContainSingle();
    }

    // Настраиваемый порог (Enforcement.KillGraceSeconds): при 30с сеанс возрастом
    // 20с (который при дефолте 15с был бы убит) ещё не завершается.
    [Fact]
    public async Task Respects_configured_grace_period()
    {
        var (cluster, audit) = await RunAsync(
            newestStartedAtUtc: ClockNow.AddSeconds(-20),
            newestUserName: "user-newest",
            graceSeconds: 30);

        await cluster.DidNotReceive().KillSessionAsync(Arg.Any<SessionDescriptor>(), Arg.Any<CancellationToken>(), Arg.Any<string?>());
        audit.Entries.Should().BeEmpty();
    }

    // Пустой user-name (базовая/однопользовательская ИБ) → в записи аудита метка
    // вместо пустоты.
    [Fact]
    public async Task Empty_username_renders_fallback_label_in_audit()
    {
        var (_, audit) = await RunAsync(
            newestStartedAtUtc: ClockNow.AddSeconds(-20),
            newestUserName: "");

        audit.Entries.Should().ContainSingle();
        audit.Entries[0].Description.Should().Contain($"пользователь {SessionDisplay.NoUserLabel})");
    }

    // tenant limit=1, две сессии (старая + самая свежая) → over by 1; newest-first
    // целится в свежую. Параметрами управляем её возрастом, именем и (опц.) grace.
    private static async Task<(IClusterClient cluster, TestHelpers.CapturingAuditLogger audit)> RunAsync(
        DateTime newestStartedAtUtc,
        string newestUserName,
        int? graceSeconds = null)
    {
        var tenantId = Guid.NewGuid();
        var clusterInfobaseId = Guid.NewGuid();

        var sessions = new[]
        {
            new SnapshotSessionEntry(Guid.NewGuid(), clusterInfobaseId, tenantId, "Acme", "БП",
                "1CV8C", "user-old", "WS01", LicenseStatus.Consuming, ClockNow.AddMinutes(-30)),
            new SnapshotSessionEntry(Guid.NewGuid(), clusterInfobaseId, tenantId, "Acme", "БП",
                "1CV8C", newestUserName, "WS01", LicenseStatus.Consuming, newestStartedAtUtc),
        };

        var payload = new SnapshotPayload(sessions, ClockNow, 10, "Rest", LicenseFactAvailable: true);

        var freshSessions = sessions.Select(s => new ClusterSession(
            s.SessionId, s.ClusterInfobaseId, s.AppId, s.UserName, s.Host,
            s.StartedAtUtc)).ToList();

        var cluster = Substitute.For<IClusterClient>();
        cluster.ListActiveSessionsAsync(Arg.Any<CancellationToken>()).Returns(freshSessions);
        cluster.KillSessionAsync(Arg.Any<SessionDescriptor>(), Arg.Any<CancellationToken>(), Arg.Any<string?>())
            .Returns(new KillSessionResult(Killed: true, AlreadyGone: false));

        var audit = new TestHelpers.CapturingAuditLogger();

        using var db = TestHelpers.NewInMemoryDb();
        db.Tenants.Add(new Tenant
        {
            Id = tenantId,
            Name = "Acme",
            MaxConcurrentLicenses = 1,
            IsActive = true,
            CreatedAt = ClockNow,
        });
        await db.SaveChangesAsync();

        var settings = Substitute.For<ISettingsSnapshot>();
        if (graceSeconds is not null)
            settings.GetInt(SettingKey.EnforcementKillGraceSeconds).Returns(graceSeconds);

        var enforcer = new KillEnforcer(
            cluster, audit, db, settings, TestHelpers.FixedClock(ClockNow),
            TestMetrics.Reconciliation(), NullLogger<KillEnforcer>.Instance);

        await enforcer.EnforceAsync(payload, freshSessions: null, CancellationToken.None);

        return (cluster, audit);
    }
}
