using System.Data.Common;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using MitLicenseCenter.Application.Publishing;
using MitLicenseCenter.Application.Settings;
using MitLicenseCenter.Domain.Infobases;
using MitLicenseCenter.Domain.Publications;
using MitLicenseCenter.Domain.Tenants;
using MitLicenseCenter.Infrastructure.Jobs;
using MitLicenseCenter.Infrastructure.Persistence;
using MitLicenseCenter.Tests.Unit.Endpoints;
using NSubstitute;
using Xunit;
using Xunit.Abstractions;

namespace MitLicenseCenter.Tests.Unit.Jobs;

// PERF-07 (MLC-043): контракт «один проход drift-job'а грузит публикации ОДНИМ
// запросом, а не N+1». Меряем реальные SQL round-trip'ы на relational-провайдере
// (SQLite — та же EF-трансляция, что у MSSQL; число запросов провайдер-независимо).
// До рефакторинга RunAllAsync делал 1 SELECT(Id) + N×SELECT(*) на каждую публикацию
// (N+1 загрузочных запросов); после — 1 проекционный SELECT на весь объём.
public sealed class DriftCheckBatchQueryTests
{
    private readonly ITestOutputHelper _output;

    public DriftCheckBatchQueryTests(ITestOutputHelper output) => _output = output;

    [Theory]
    [InlineData(25)]
    public async Task RunAll_loads_all_publications_in_single_query(int publicationCount)
    {
        var capture = new SqlCapture();
        using var sqlite = TestHelpers.SqliteTestDb.Create(capture);

        await SeedAsync(sqlite, publicationCount);

        // BEFORE (старая форма загрузки, воспроизведена in-line на тех же данных):
        // 1 запрос Id + N запросов FirstOrDefault на каждую публикацию = N+1.
        capture.Reset();
        await using (var ctx = sqlite.NewContext())
        {
            var ids = await ctx.Publications.AsNoTracking().Select(p => p.Id).ToListAsync();
            foreach (var id in ids)
            {
                _ = await ctx.Publications.FirstOrDefaultAsync(x => x.Id == id);
            }
        }
        var beforeLoadSelects = capture.PublicationLoadSelects;

        // AFTER (актуальный RunAllAsync): один проекционный SELECT на весь объём.
        capture.Reset();
        await using (var ctx = sqlite.NewContext())
        {
            var job = NewJob(ctx);
            await job.RunAllAsync(CancellationToken.None);
        }
        var afterLoadSelects = capture.PublicationLoadSelects;

        _output.WriteLine($"Publications seeded: {publicationCount}");
        _output.WriteLine($"BEFORE — загрузочных SELECT'ов на проход: {beforeLoadSelects} (= N+1)");
        _output.WriteLine($"AFTER  — загрузочных SELECT'ов на проход: {afterLoadSelects}");

        beforeLoadSelects.Should().Be(publicationCount + 1, "старая форма делала 1 SELECT(Id) + N×FirstOrDefault");
        afterLoadSelects.Should().Be(1, "новая форма грузит все публикации одним проекционным запросом (N+1 → 1)");
    }

    private static DriftCheckJob NewJob(AppDbContext ctx)
    {
        var settings = Substitute.For<ISettingsSnapshot>();
        settings.GetInt(Arg.Any<string>()).Returns((int?)null);

        return new DriftCheckJob(
            ctx,
            new InSyncIis(),
            new TestHelpers.CapturingAuditLogger(),
            settings,
            new DriftThrottleState(),
            TimeProvider.System,
            NullLogger<DriftCheckJob>.Instance);
    }

    private static async Task SeedAsync(TestHelpers.SqliteTestDb sqlite, int count)
    {
        await using var ctx = sqlite.NewContext();
        var tenant = new Tenant
        {
            Id = Guid.NewGuid(),
            Name = "Acme",
            MaxConcurrentLicenses = 10,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
        };
        ctx.Tenants.Add(tenant);

        for (var i = 0; i < count; i++)
        {
            var infobaseId = Guid.NewGuid();
            ctx.Infobases.Add(new Infobase
            {
                Id = infobaseId,
                TenantId = tenant.Id,
                Name = $"Acme BP {i}",
                ClusterInfobaseId = Guid.NewGuid(),
                DatabaseServer = "sql.local",
                DatabaseName = $"acme_bp_{i}",
                Status = InfobaseStatus.Active,
                CreatedAt = DateTime.UtcNow,
            });
            ctx.Publications.Add(new Publication
            {
                Id = Guid.NewGuid(),
                InfobaseId = infobaseId,
                SiteName = "Default Web Site",
                VirtualPath = $"/Pub{i}",
                PlatformVersion = "8.3.23.1865",
                EnableOData = true,
                EnableHttpServices = true,
                CreatedAt = DateTime.UtcNow,
                LastDriftStatus = PublicationDriftStatus.InSync,
            });
        }

        await ctx.SaveChangesAsync();
    }

    // Echo-stub: actual == desired ⇒ Compare всегда InSync ⇒ нет audit-строк и
    // tenant-lookup'ов, поэтому счётчик ловит только загрузку+запись (чистый замер).
    private sealed class InSyncIis : IIisPublishingService
    {
        public Task<PublicationActualState> ReadActualStateAsync(Publication p, CancellationToken ct) =>
            Task.FromResult(new PublicationActualState(
                SiteExists: true,
                VirtualPathExists: true,
                PlatformVersion: p.PlatformVersion,
                EnableOData: p.EnableOData,
                EnableHttpServices: p.EnableHttpServices,
                VrdContent: "<point/>",
                Error: null));

        public Task ApplyDesiredStateAsync(Publication p, CancellationToken ct) => Task.CompletedTask;

        public Task<IReadOnlyList<IisSiteInfo>> ListSitesAsync(CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<IisSiteInfo>>([]);
    }

    // Считает реальные SQL-команды; PublicationLoadSelects = число SELECT'ов,
    // загружающих строки таблицы Publications (форма загрузки, а не запись/аудит).
    private sealed class SqlCapture : DbCommandInterceptor
    {
        private readonly List<string> _commands = [];
        private readonly object _gate = new();

        public void Reset()
        {
            lock (_gate) _commands.Clear();
        }

        public int PublicationLoadSelects
        {
            get
            {
                lock (_gate)
                {
                    return _commands.Count(IsPublicationLoadSelect);
                }
            }
        }

        private static bool IsPublicationLoadSelect(string sql)
        {
            var trimmed = sql.TrimStart();
            return trimmed.StartsWith("SELECT", StringComparison.OrdinalIgnoreCase)
                && sql.Contains("\"Publications\"", StringComparison.OrdinalIgnoreCase);
        }

        private void Add(DbCommand command)
        {
            lock (_gate) _commands.Add(command.CommandText);
        }

        public override InterceptionResult<DbDataReader> ReaderExecuting(
            DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result)
        {
            Add(command);
            return base.ReaderExecuting(command, eventData, result);
        }

        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
            DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result,
            CancellationToken cancellationToken = default)
        {
            Add(command);
            return base.ReaderExecutingAsync(command, eventData, result, cancellationToken);
        }

        public override InterceptionResult<int> NonQueryExecuting(
            DbCommand command, CommandEventData eventData, InterceptionResult<int> result)
        {
            Add(command);
            return base.NonQueryExecuting(command, eventData, result);
        }

        public override ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(
            DbCommand command, CommandEventData eventData, InterceptionResult<int> result,
            CancellationToken cancellationToken = default)
        {
            Add(command);
            return base.NonQueryExecutingAsync(command, eventData, result, cancellationToken);
        }

        public override InterceptionResult<object> ScalarExecuting(
            DbCommand command, CommandEventData eventData, InterceptionResult<object> result)
        {
            Add(command);
            return base.ScalarExecuting(command, eventData, result);
        }

        public override ValueTask<InterceptionResult<object>> ScalarExecutingAsync(
            DbCommand command, CommandEventData eventData, InterceptionResult<object> result,
            CancellationToken cancellationToken = default)
        {
            Add(command);
            return base.ScalarExecutingAsync(command, eventData, result, cancellationToken);
        }
    }
}
