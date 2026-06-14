using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using MitLicenseCenter.Domain.Audit;
using MitLicenseCenter.Infrastructure.Audit;
using MitLicenseCenter.Infrastructure.Persistence;
using Xunit;

namespace MitLicenseCenter.Tests.Unit.Audit;

public sealed class AuditLogEnumMappingTests
{
    [Fact]
    public async Task ActionType_and_Reason_round_trip_through_DbContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"audit-enum-{Guid.NewGuid():N}")
            .Options;

        using (var db = new AppDbContext(options))
        {
            db.AuditLogs.Add(new AuditLog
            {
                Id = Guid.NewGuid(),
                Timestamp = DateTime.UtcNow,
                ActionType = AuditActionType.TenantCreated,
                Reason = AuditReason.ManualByAdmin,
                Initiator = "admin",
                Description = "Клиент «Acme» создан.",
                TenantId = Guid.NewGuid(),
            });

            await db.SaveChangesAsync();
        }

        using (var db = new AppDbContext(options))
        {
            var saved = await db.AuditLogs.SingleAsync();
            saved.ActionType.Should().Be(AuditActionType.TenantCreated);
            saved.Reason.Should().Be(AuditReason.ManualByAdmin);
        }
    }

    // BE-14 (MLC-120) — ПОЛНАЯ таблица freeze: каждое имя↔int (все 43 члена), включая
    // замороженные исторические (drift/circuit), оживлённые (LimitChanged=201) и Iis-серию.
    // Инвариант «enum заморожен» из CLAUDE.md: случайная смена незакреплённого числа должна
    // ронять тест. Дополнительно EnumTableCountMatchesDeclaredMembers ловит ДОБАВЛЕНИЕ
    // нового члена без строки в этой таблице.
    [Theory]
    [InlineData(AuditActionType.TenantCreated, 1)]
    [InlineData(AuditActionType.TenantUpdated, 2)]
    [InlineData(AuditActionType.TenantDeleted, 3)]
    [InlineData(AuditActionType.InfobaseCreated, 10)]
    [InlineData(AuditActionType.InfobaseUpdated, 11)]
    [InlineData(AuditActionType.InfobaseDeleted, 12)]
    [InlineData(AuditActionType.InfobaseReassigned, 13)]
    [InlineData(AuditActionType.UnassignedInfobaseHidden, 14)]
    [InlineData(AuditActionType.UnassignedInfobaseUnhidden, 15)]
    [InlineData(AuditActionType.PublicationCreated, 20)]
    [InlineData(AuditActionType.PublicationUpdated, 21)]
    [InlineData(AuditActionType.PublicationDeleted, 22)]
    [InlineData(AuditActionType.PublicationUnpublished, 23)]
    [InlineData(AuditActionType.AdminLoggedIn, 100)]
    [InlineData(AuditActionType.AdminLoggedOut, 101)]
    [InlineData(AuditActionType.AdminPasswordChanged, 102)]
    [InlineData(AuditActionType.UserCreated, 103)]
    [InlineData(AuditActionType.UserDisabled, 104)]
    [InlineData(AuditActionType.UserPasswordReset, 105)]
    [InlineData(AuditActionType.UserEnabled, 106)]
    [InlineData(AuditActionType.UserRoleChanged, 107)]
    [InlineData(AuditActionType.LoginFailed, 108)]
    [InlineData(AuditActionType.SessionKilled, 200)]
    // MLC-119 (BE-11) — LimitChanged оживлён, но int=201 заморожен (frozen-int rule).
    [InlineData(AuditActionType.LimitChanged, 201)]
    [InlineData(AuditActionType.PublicationDriftDetected, 210)]
    [InlineData(AuditActionType.PublicationReconciled, 211)]
    [InlineData(AuditActionType.PublicationPublished, 212)]
    [InlineData(AuditActionType.PublicationPlatformChanged, 213)]
    [InlineData(AuditActionType.IisApplicationPoolRecycled, 220)]
    [InlineData(AuditActionType.IisApplicationPoolStarted, 221)]
    [InlineData(AuditActionType.IisApplicationPoolStopped, 222)]
    [InlineData(AuditActionType.IisSiteStarted, 223)]
    [InlineData(AuditActionType.IisSiteStopped, 224)]
    [InlineData(AuditActionType.IisSiteRestarted, 225)]
    [InlineData(AuditActionType.IisReset, 226)]
    [InlineData(AuditActionType.IisStopped, 227)]
    [InlineData(AuditActionType.IisStarted, 228)]
    [InlineData(AuditActionType.ClusterAdapterCircuitOpened, 300)]
    [InlineData(AuditActionType.ClusterAdapterCircuitClosed, 301)]
    [InlineData(AuditActionType.SettingChanged, 400)]
    [InlineData(AuditActionType.AuditLogsPurged, 500)]
    [InlineData(AuditActionType.BackupRequested, 510)]
    [InlineData(AuditActionType.BackupSucceeded, 511)]
    [InlineData(AuditActionType.BackupFailed, 512)]
    [InlineData(AuditActionType.BackupDeleted, 513)]
    [InlineData(AuditActionType.BackupsPurged, 514)]
    // MLC-159 (ADR-47) — 600-серия управления службой RAS (frozen-int).
    [InlineData(AuditActionType.RasServiceRegistered, 600)]
    [InlineData(AuditActionType.RasServiceUpdated, 601)]
    [InlineData(AuditActionType.RasServiceStarted, 602)]
    public void AuditActionType_int_values_are_stable(AuditActionType action, int expected)
    {
        ((int)action).Should().Be(expected);
    }

    // BE-14 — гарантирует, что таблица freeze выше покрывает ВСЕ объявленные члены enum.
    // Если в будущем добавят новый член без строки [InlineData], этот тест упадёт и заставит
    // зафиксировать его int явно (frozen-int rule из CLAUDE.md).
    [Fact]
    public void EnumTableCountMatchesDeclaredMembers()
    {
        var declaredCount = Enum.GetValues<AuditActionType>().Length;

        var coveredCount = typeof(AuditLogEnumMappingTests)
            .GetMethod(nameof(AuditActionType_int_values_are_stable))!
            .GetCustomAttributes(typeof(InlineDataAttribute), false)
            .Length;

        coveredCount.Should().Be(declaredCount,
            "каждый член AuditActionType должен иметь строку [InlineData] в freeze-таблице");
    }

    [Theory]
    [InlineData(AuditReason.LimitExceeded, 1)]
    [InlineData(AuditReason.ManualByAdmin, 2)]
    public void AuditReason_int_values_are_stable(AuditReason reason, int expected)
    {
        ((int)reason).Should().Be(expected);
    }
}
