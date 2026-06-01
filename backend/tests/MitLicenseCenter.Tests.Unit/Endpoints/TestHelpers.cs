using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using MitLicenseCenter.Application.Auditing;
using MitLicenseCenter.Domain.Audit;
using MitLicenseCenter.Infrastructure.Persistence;

namespace MitLicenseCenter.Tests.Unit.Endpoints;

internal static class TestHelpers
{
    public static AppDbContext NewInMemoryDb(string? name = null, IInterceptor? interceptor = null)
    {
        var builder = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(name ?? $"endpoint-{Guid.NewGuid():N}");
        if (interceptor is not null)
        {
            builder.AddInterceptors(interceptor);
        }
        return new AppDbContext(builder.Options);
    }

    // MLC-004 — EF InMemory не воспроизводит нарушение уникального индекса (это MLC-008),
    // поэтому гонку эмулируем перехватчиком: на следующем SaveChanges он бросает заранее
    // подготовленное DbUpdateException (как это сделал бы SQL Server). Перехватчик
    // изначально «обезоружен» — сидинг проходит штатно; тест взводит Armed перед вызовом
    // endpoint'а, чтобы выстрелило именно на сохранении операции.
    public sealed class ThrowOnSaveInterceptor : SaveChangesInterceptor
    {
        private readonly Exception _toThrow;

        public ThrowOnSaveInterceptor(Exception toThrow) => _toThrow = toThrow;

        public bool Armed { get; set; }

        public override InterceptionResult<int> SavingChanges(
            DbContextEventData eventData, InterceptionResult<int> result)
        {
            if (Armed)
            {
                throw _toThrow;
            }
            return base.SavingChanges(eventData, result);
        }

        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            if (Armed)
            {
                throw _toThrow;
            }
            return base.SavingChangesAsync(eventData, result, cancellationToken);
        }
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
