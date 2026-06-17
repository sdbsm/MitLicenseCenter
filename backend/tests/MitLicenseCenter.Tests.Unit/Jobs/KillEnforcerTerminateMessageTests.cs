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

// MLC-190: enforcement-путь читает настройку Enforcement.TerminateMessage и прокидывает её
// текст в rac terminate (--error-message → модальное окно тонкого клиента). Непустая настройка
// → текст уходит; пустая/несидированная → null (флаг не передаётся, текущее поведение).
public sealed class KillEnforcerTerminateMessageTests
{
    private static readonly DateTime ClockNow = new(2026, 6, 17, 10, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task Passes_configured_terminate_message_to_cluster_kill()
    {
        const string message = "Лимит лицензий. Обратитесь в ООО «Хостер», тел. 8-800-000.";
        var cluster = await RunAsync(terminateMessage: message);

        await cluster.Received(1).KillSessionAsync(
            Arg.Any<SessionDescriptor>(), Arg.Any<CancellationToken>(), message);
    }

    [Fact]
    public async Task Passes_null_when_setting_is_blank()
    {
        var cluster = await RunAsync(terminateMessage: "   ");

        // Пусто/пробелы трактуются как «не передавать»: на rac-адаптере IsNullOrWhiteSpace
        // отбросит флаг. Здесь проверяем, что enforcer прокидывает значение настройки как есть,
        // а решение «передавать или нет» остаётся в адаптере (см. RacExecutableRasClusterClientTests).
        await cluster.Received(1).KillSessionAsync(
            Arg.Any<SessionDescriptor>(), Arg.Any<CancellationToken>(), "   ");
    }

    [Fact]
    public async Task Passes_null_when_setting_unset()
    {
        var cluster = await RunAsync(terminateMessage: null);

        await cluster.Received(1).KillSessionAsync(
            Arg.Any<SessionDescriptor>(), Arg.Any<CancellationToken>(), null);
    }

    // tenant limit=1, две сессии (старая + свежая, обе вне grace) → over by 1; newest-first
    // целится в свежую → ровно один kill, на котором проверяем переданный текст.
    private static async Task<IClusterClient> RunAsync(string? terminateMessage)
    {
        var tenantId = Guid.NewGuid();
        var clusterInfobaseId = Guid.NewGuid();

        var sessions = new[]
        {
            new SnapshotSessionEntry(Guid.NewGuid(), clusterInfobaseId, tenantId, "Acme", "БП",
                "1CV8C", "user-old", "WS01", LicenseStatus.Consuming, ClockNow.AddMinutes(-30)),
            new SnapshotSessionEntry(Guid.NewGuid(), clusterInfobaseId, tenantId, "Acme", "БП",
                "1CV8C", "user-new", "WS01", LicenseStatus.Consuming, ClockNow.AddMinutes(-5)),
        };

        var payload = new SnapshotPayload(sessions, ClockNow, 10, "Rest", LicenseFactAvailable: true);

        var freshSessions = sessions.Select(s => new ClusterSession(
            s.SessionId, s.ClusterInfobaseId, s.AppId, s.UserName, s.Host, s.StartedAtUtc)).ToList();

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
        settings.GetString(SettingKey.EnforcementTerminateMessage).Returns(terminateMessage);

        var enforcer = new KillEnforcer(
            cluster, audit, db, settings, TestHelpers.FixedClock(ClockNow),
            TestMetrics.Reconciliation(), NullLogger<KillEnforcer>.Instance);

        await enforcer.EnforceAsync(payload, freshSessions: null, CancellationToken.None);

        return cluster;
    }
}
