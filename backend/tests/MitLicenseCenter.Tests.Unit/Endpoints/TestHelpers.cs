using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
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

    // MLC-008 — Реальный провайдер для контрактных тестов persistence-инвариантов.
    // EF InMemory (NewInMemoryDb) НЕ соблюдает unique-индексы, FK-cascade/SetNull/Restrict
    // и конкурентность — поэтому центральные доменные инварианты (per-tenant имя,
    // глобальная уникальность кластер-базы, каскад публикации) на нём непроверяемы.
    // SQLite-in-memory строит схему из той же модели через EnsureCreated (миграции —
    // SQL-Server-специфичны, для SQLite неприменимы) и реально применяет индексы и FK.
    //
    // База живёт ровно пока открыто соединение ("DataSource=:memory:"), поэтому
    // соединение держится открытым на всё время теста, а несколько контекстов
    // (NewContext) разделяют одну БД — это позволяет проверять поведение на стороне БД,
    // а не в change-tracker'е EF (удаление в «чистом» контексте, не отслеживающем зависимые
    // сущности, заставляет каскад/SetNull выполнить именно СУБД). `Foreign Keys=True`
    // включает PRAGMA foreign_keys для каждого соединения (в SQLite FK по умолчанию выкл.).
    public sealed class SqliteTestDb : IDisposable
    {
        private readonly SqliteConnection _connection;
        private readonly DbContextOptions<AppDbContext> _options;

        private SqliteTestDb(SqliteConnection connection, DbContextOptions<AppDbContext> options)
        {
            _connection = connection;
            _options = options;
        }

        public static SqliteTestDb Create(params IInterceptor[] interceptors)
            => CreateCore(configureSqlite: null, interceptors);

        // MLC-074: позволяет навесить на SQLite-контекст кастомную execution strategy
        // (напр. RetriesOnFailure=true), чтобы воспроизвести прод-гард
        // SqlServerRetryingExecutionStrategy, который SQLite-по-умолчанию
        // (NonRetryingExecutionStrategy) не даёт проверить.
        public static SqliteTestDb Create(
            Action<SqliteDbContextOptionsBuilder> configureSqlite,
            params IInterceptor[] interceptors)
            => CreateCore(configureSqlite, interceptors);

        private static SqliteTestDb CreateCore(
            Action<SqliteDbContextOptionsBuilder>? configureSqlite,
            IInterceptor[] interceptors)
        {
            var connection = new SqliteConnection("DataSource=:memory:;Foreign Keys=True");
            connection.Open();
            var builder = new DbContextOptionsBuilder<AppDbContext>()
                .UseSqlite(connection, sqlite => configureSqlite?.Invoke(sqlite))
                .ReplaceService<IModelCustomizer, SqliteModelCustomizer>();
            if (interceptors.Length > 0)
            {
                builder.AddInterceptors(interceptors);
            }
            var options = builder.Options;
            using (var ctx = new AppDbContext(options))
            {
                ctx.Database.EnsureCreated();
            }
            return new SqliteTestDb(connection, options);
        }

        public AppDbContext NewContext() => new(_options);

        // MLC-237: открытое соединение SQLite-БД — нужно concurrency-тесту Investigation.RowVersion, чтобы
        // навесить триггер, эмулирующий серверный bump rowversion на UPDATE (SQLite сам токен не трогает).
        public SqliteConnection Connection => _connection;

        public void Dispose() => _connection.Dispose();
    }

    // Модель использует SQL-Server-специфичный тип колонки `varbinary(max)`
    // (DPAPI-payload в dbo.Settings.Value) — SQLite его DDL не парсит. Этот кастомайзер
    // применяется ТОЛЬКО к тестовым SQLite-опциям (через ReplaceService) и переписывает
    // такие типы в нативный SQLite `BLOB`; продакшн-модель (AppDbContext) не трогается.
    // Контрактные инварианты (unique-индексы, FK-поведение) к типу колонки не относятся —
    // они остаются ровно теми, что задал AppDbContext.OnModelCreating.
    private sealed class SqliteModelCustomizer : RelationalModelCustomizer
    {
        public SqliteModelCustomizer(ModelCustomizerDependencies dependencies)
            : base(dependencies)
        {
        }

        public override void Customize(ModelBuilder modelBuilder, DbContext context)
        {
            base.Customize(modelBuilder, context);

            foreach (var property in modelBuilder.Model.GetEntityTypes()
                         .SelectMany(e => e.GetProperties()))
            {
                var columnType = property.GetColumnType();
                if (columnType is not null &&
                    (columnType.Contains("varbinary", StringComparison.OrdinalIgnoreCase) ||
                     // MLC-237: Investigation.RowVersion маппит на SQL-Server `rowversion` — SQLite такого
                     // типа не знает, переписываем на нативный BLOB (как varbinary). Контрактные инварианты
                     // (concurrency-токен энфорсится триггером в тесте) к типу колонки не относятся.
                     columnType.Contains("rowversion", StringComparison.OrdinalIgnoreCase)))
                {
                    property.SetColumnType("BLOB");
                }
            }
        }
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

        // MLC-119 — enlist захватываем в тот же список, синхронно (тест-дубль не моделирует
        // отложенный SaveChanges; для проверок состава/числа записей этого достаточно).
        public void Enlist(
            AuditActionType action,
            string initiator,
            string description,
            Guid? tenantId = null,
            AuditReason? reason = null)
        {
            Entries.Add((action, initiator, description, tenantId, reason));
        }
    }

    private sealed class FixedTimeProvider : TimeProvider
    {
        private readonly DateTimeOffset _now;
        public FixedTimeProvider(DateTimeOffset now) => _now = now;
        public override DateTimeOffset GetUtcNow() => _now;
    }

    // MLC-074: тест-дубль ретраящей execution strategy. Прод включает
    // EnableRetryOnFailure → SqlServerRetryingExecutionStrategy, у которой
    // RetriesOnFailure=true; на такой стратегии ручной BeginTransaction вне
    // CreateExecutionStrategy().ExecuteAsync бросает "...does not support
    // user-initiated transactions" (CoreStrings.ExecutionStrategyExistingTransaction).
    // SQLite по умолчанию использует NonRetryingExecutionStrategy (RetriesOnFailure=false),
    // поэтому штатные тесты ретеншена дыру не ловят (дефект класса MLC-008).
    // maxRetryCount>0 → RetriesOnFailure=true; ShouldRetryOn=>false — гард активен,
    // но реальные исключения наружу не глотаются повторами.
    //
    // onFirstExecution (опц.) вызывается на каждом запуске стратегии — нужен
    // AuditRetentionJob-тесту: его raw `ExecuteSql` обходит стратегию (идёт прямо в
    // RelationalCommand), поэтому до фикса конфигурированная стратегия не вызывается
    // НИ разу; после фикса батч обёрнут в CreateExecutionStrategy().ExecuteAsync →
    // счётчик > 0. Это и есть дискриминатор бага для raw-SQL-джобы.
    public sealed class RetriesOnFailureExecutionStrategy : ExecutionStrategy
    {
        private readonly Action? _onFirstExecution;

        public RetriesOnFailureExecutionStrategy(
            ExecutionStrategyDependencies dependencies,
            Action? onFirstExecution = null)
            : base(dependencies, maxRetryCount: 3, maxRetryDelay: TimeSpan.Zero)
            => _onFirstExecution = onFirstExecution;

        protected override bool ShouldRetryOn(Exception exception) => false;

        protected override void OnFirstExecution()
        {
            _onFirstExecution?.Invoke();
            base.OnFirstExecution();
        }
    }
}
