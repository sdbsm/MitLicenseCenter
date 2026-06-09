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

    [Theory]
    [InlineData(AuditActionType.TenantCreated, 1)]
    [InlineData(AuditActionType.TenantUpdated, 2)]
    [InlineData(AuditActionType.TenantDeleted, 3)]
    [InlineData(AuditActionType.AdminLoggedIn, 100)]
    [InlineData(AuditActionType.AdminLoggedOut, 101)]
    [InlineData(AuditActionType.AdminPasswordChanged, 102)]
    [InlineData(AuditActionType.BackupRequested, 510)]
    [InlineData(AuditActionType.BackupSucceeded, 511)]
    [InlineData(AuditActionType.BackupFailed, 512)]
    [InlineData(AuditActionType.BackupDeleted, 513)]
    [InlineData(AuditActionType.BackupsPurged, 514)]
    public void AuditActionType_int_values_are_stable(AuditActionType action, int expected)
    {
        ((int)action).Should().Be(expected);
    }

    [Theory]
    [InlineData(AuditReason.LimitExceeded, 1)]
    [InlineData(AuditReason.ManualByAdmin, 2)]
    public void AuditReason_int_values_are_stable(AuditReason reason, int expected)
    {
        ((int)reason).Should().Be(expected);
    }
}
