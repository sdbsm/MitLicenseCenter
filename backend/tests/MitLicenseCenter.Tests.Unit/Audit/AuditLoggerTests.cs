using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MitLicenseCenter.Application.Auditing;
using MitLicenseCenter.Domain.Audit;
using MitLicenseCenter.Infrastructure.Audit;
using MitLicenseCenter.Infrastructure.Persistence;
using Xunit;

namespace MitLicenseCenter.Tests.Unit.Audit;

public sealed class AuditLoggerTests
{
    [Fact]
    public async Task LogAsync_persists_AuditLog_row_with_provided_fields()
    {
        var dbName = $"audit-logger-{Guid.NewGuid():N}";

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(dbName)
            .Options;

        using var db = new AppDbContext(options);
        var fixedTime = new DateTimeOffset(2026, 5, 18, 10, 0, 0, TimeSpan.Zero);
        var clock = new FrozenClock(fixedTime);
        var sut = new AuditLogger(db, clock);

        var tenantId = Guid.NewGuid();
        await sut.LogAsync(
            AuditActionType.TenantCreated,
            initiator: "admin",
            description: "Клиент «Acme» создан.",
            tenantId: tenantId,
            reason: AuditReason.ManualByAdmin);

        var entry = await db.AuditLogs.SingleAsync();
        entry.ActionType.Should().Be(AuditActionType.TenantCreated);
        entry.Initiator.Should().Be("admin");
        entry.Description.Should().Be("Клиент «Acme» создан.");
        entry.TenantId.Should().Be(tenantId);
        entry.Reason.Should().Be(AuditReason.ManualByAdmin);
        entry.Timestamp.Should().Be(fixedTime.UtcDateTime);
    }

    [Fact]
    public async Task LogAsync_rejects_empty_initiator()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"audit-logger-{Guid.NewGuid():N}")
            .Options;

        using var db = new AppDbContext(options);
        var sut = new AuditLogger(db, TimeProvider.System);

        var act = async () => await sut.LogAsync(AuditActionType.AdminLoggedIn, " ", "desc");
        await act.Should().ThrowAsync<ArgumentException>();
    }

    private sealed class FrozenClock : TimeProvider
    {
        private readonly DateTimeOffset _now;
        public FrozenClock(DateTimeOffset now) => _now = now;
        public override DateTimeOffset GetUtcNow() => _now;
    }
}
