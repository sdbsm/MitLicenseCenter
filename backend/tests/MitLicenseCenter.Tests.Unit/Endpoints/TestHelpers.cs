using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using MitLicenseCenter.Application.Auditing;
using MitLicenseCenter.Domain.Audit;
using MitLicenseCenter.Infrastructure.Persistence;

namespace MitLicenseCenter.Tests.Unit.Endpoints;

internal static class TestHelpers
{
    public static AppDbContext NewInMemoryDb(string? name = null)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(name ?? $"endpoint-{Guid.NewGuid():N}")
            .Options;
        return new AppDbContext(options);
    }

    public static DefaultHttpContext NewHttpContext(string userName = "admin")
    {
        var ctx = new DefaultHttpContext();
        var identity = new ClaimsIdentity(
            [new Claim(ClaimTypes.Name, userName)],
            authenticationType: "Test");
        ctx.User = new ClaimsPrincipal(identity);
        return ctx;
    }

    public static TimeProvider FixedClock(DateTime utc) =>
        new FixedTimeProvider(new DateTimeOffset(utc, TimeSpan.Zero));

    public sealed class CapturingAuditLogger : IAuditLogger
    {
        public List<(AuditActionType Action, string Initiator, string Description, Guid? TenantId, AuditReason? Reason)> Entries { get; } = [];

        public Task LogAsync(
            AuditActionType action,
            string initiator,
            string description,
            Guid? tenantId = null,
            AuditReason? reason = null,
            CancellationToken ct = default)
        {
            Entries.Add((action, initiator, description, tenantId, reason));
            return Task.CompletedTask;
        }
    }

    private sealed class FixedTimeProvider : TimeProvider
    {
        private readonly DateTimeOffset _now;
        public FixedTimeProvider(DateTimeOffset now) => _now = now;
        public override DateTimeOffset GetUtcNow() => _now;
    }
}
