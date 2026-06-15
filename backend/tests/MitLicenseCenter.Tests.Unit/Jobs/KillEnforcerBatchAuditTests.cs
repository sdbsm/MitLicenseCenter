using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using MitLicenseCenter.Application.Clusters;
using MitLicenseCenter.Application.Sessions;
using MitLicenseCenter.Application.Settings;
using MitLicenseCenter.Domain.Audit;
using MitLicenseCenter.Domain.Tenants;
using MitLicenseCenter.Infrastructure.Audit;
using MitLicenseCenter.Infrastructure.Jobs;
using MitLicenseCenter.Tests.Unit.Diagnostics;
using MitLicenseCenter.Tests.Unit.Endpoints;
using NSubstitute;
using Xunit;

namespace MitLicenseCenter.Tests.Unit.Jobs;

// MLC-119 (BE-25) — N убийств за цикл пишут N SessionKilled-записей ОДНИМ SaveChanges
// (enlist в общий контекст + единый round-trip после цикла), а не N round-trip'ами под
// замком. Прогон на продакшн-AuditLogger поверх того же db, со счётчиком SaveChanges.
public sealed class KillEnforcerBatchAuditTests
{
    // Считает фактические SaveChanges по контексту (round-trip'ы в БД).
    private sealed class CountingSaveInterceptor : SaveChangesInterceptor
    {
        public int SavedChangesCount { get; private set; }

        public override int SavedChanges(SaveChangesCompletedEventData eventData, int result)
        {
            SavedChangesCount++;
            return base.SavedChanges(eventData, result);
        }

        public override ValueTask<int> SavedChangesAsync(
            SaveChangesCompletedEventData eventData, int result, CancellationToken cancellationToken = default)
        {
            SavedChangesCount++;
            return base.SavedChangesAsync(eventData, result, cancellationToken);
        }
    }

    [Fact]
    public async Task Multiple_kills_persist_all_SessionKilled_rows_in_a_single_SaveChanges()
    {
        var tenantId = Guid.NewGuid();
        var clusterInfobaseId = Guid.NewGuid();
        var baseTime = new DateTime(2026, 6, 13, 10, 0, 0, DateTimeKind.Utc);

        // limit=1, 5 сессий → 4 over-limit → 4 kill.
        var sessions = Enumerable.Range(0, 5).Select(i => new SnapshotSessionEntry(
            SessionId: Guid.NewGuid(),
            ClusterInfobaseId: clusterInfobaseId,
            TenantId: tenantId,
            TenantName: "Acme",
            InfobaseName: "БП",
            AppId: "1CV8C",
            UserName: $"user{i}",
            Host: "WS01",
            LicenseStatus: LicenseStatus.Consuming,
            StartedAtUtc: baseTime.AddMinutes(i))).ToList();

        var payload = new SnapshotPayload(sessions, baseTime.AddMinutes(30), 42, "Rest", LicenseFactAvailable: true);

        var freshSessions = sessions.Select(s => new ClusterSession(
            s.SessionId, s.ClusterInfobaseId, s.AppId, s.UserName, s.Host, s.StartedAtUtc))
            .ToList();

        var cluster = Substitute.For<IClusterClient>();
        cluster.ListActiveSessionsAsync(Arg.Any<CancellationToken>()).Returns(freshSessions);
        cluster.KillSessionAsync(Arg.Any<SessionDescriptor>(), Arg.Any<CancellationToken>())
            .Returns(new KillSessionResult(Killed: true, AlreadyGone: false));

        var counter = new CountingSaveInterceptor();
        using var db = TestHelpers.NewInMemoryDb(interceptor: counter);
        db.Tenants.Add(new Tenant
        {
            Id = tenantId,
            Name = "Acme",
            MaxConcurrentLicenses = 1,
            IsActive = true,
            CreatedAt = baseTime,
        });
        await db.SaveChangesAsync();
        var savesAfterSeed = counter.SavedChangesCount;

        // Продакшн-AuditLogger поверх ТОГО ЖЕ db — enlist идёт в общий контекст.
        var audit = new AuditLogger(db, TestHelpers.FixedClock(baseTime.AddMinutes(30)));
        var enforcer = new KillEnforcer(
            cluster, audit, db, Substitute.For<ISettingsSnapshot>(),
            TestHelpers.FixedClock(baseTime.AddMinutes(30)), TestMetrics.Reconciliation(),
            NullLogger<KillEnforcer>.Instance);

        await enforcer.EnforceAsync(payload, freshSessions: null, CancellationToken.None);

        // 4 SessionKilled-записи реально в БД.
        var killed = await db.AuditLogs.AsNoTracking()
            .Where(a => a.ActionType == AuditActionType.SessionKilled)
            .ToListAsync();
        killed.Should().HaveCount(4);
        killed.Should().AllSatisfy(a =>
        {
            a.Reason.Should().Be(AuditReason.LimitExceeded);
            a.TenantId.Should().Be(tenantId);
            a.Initiator.Should().Be("System");
        });

        await cluster.Received(4).KillSessionAsync(Arg.Any<SessionDescriptor>(), Arg.Any<CancellationToken>());

        // Ровно один SaveChanges за весь цикл enforcement (батч), а не по одному на kill.
        (counter.SavedChangesCount - savesAfterSeed).Should()
            .Be(1, "все enlist-записи цикла коммитятся одним round-trip");
    }

    [Fact]
    public async Task No_kills_means_no_SaveChanges_in_enforcement_cycle()
    {
        var tenantId = Guid.NewGuid();
        var baseTime = new DateTime(2026, 6, 13, 10, 0, 0, DateTimeKind.Utc);

        // limit=10, 2 сессии → не over-limit → ранний выход, kill нет.
        var sessions = Enumerable.Range(0, 2).Select(i => new SnapshotSessionEntry(
            SessionId: Guid.NewGuid(),
            ClusterInfobaseId: Guid.NewGuid(),
            TenantId: tenantId,
            TenantName: "Acme",
            InfobaseName: "БП",
            AppId: "1CV8C",
            UserName: $"user{i}",
            Host: "WS01",
            LicenseStatus: LicenseStatus.Consuming,
            StartedAtUtc: baseTime.AddMinutes(i))).ToList();

        var payload = new SnapshotPayload(sessions, baseTime.AddMinutes(30), 42, "Rest", LicenseFactAvailable: true);

        var cluster = Substitute.For<IClusterClient>();
        cluster.KillSessionAsync(Arg.Any<SessionDescriptor>(), Arg.Any<CancellationToken>())
            .Returns(new KillSessionResult(Killed: true, AlreadyGone: false));

        var counter = new CountingSaveInterceptor();
        using var db = TestHelpers.NewInMemoryDb(interceptor: counter);
        db.Tenants.Add(new Tenant
        {
            Id = tenantId,
            Name = "Acme",
            MaxConcurrentLicenses = 10,
            IsActive = true,
            CreatedAt = baseTime,
        });
        await db.SaveChangesAsync();
        var savesAfterSeed = counter.SavedChangesCount;

        var audit = new AuditLogger(db, TestHelpers.FixedClock(baseTime.AddMinutes(30)));
        var enforcer = new KillEnforcer(
            cluster, audit, db, Substitute.For<ISettingsSnapshot>(),
            TestHelpers.FixedClock(baseTime.AddMinutes(30)), TestMetrics.Reconciliation(),
            NullLogger<KillEnforcer>.Instance);

        await enforcer.EnforceAsync(payload, freshSessions: null, CancellationToken.None);

        (counter.SavedChangesCount - savesAfterSeed).Should()
            .Be(0, "при totalKills==0 SaveChanges не зовётся");
    }
}
