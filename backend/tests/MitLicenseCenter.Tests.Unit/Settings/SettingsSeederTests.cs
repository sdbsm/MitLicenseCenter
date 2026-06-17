using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using MitLicenseCenter.Application.Settings;
using MitLicenseCenter.Domain.Settings;
using MitLicenseCenter.Infrastructure.Persistence;
using MitLicenseCenter.Infrastructure.Settings;
using Xunit;

namespace MitLicenseCenter.Tests.Unit.Settings;

// MLC-117: идемпотентный сидер + целевой heal OneC.RAS.Endpoint. На свежей БД ключ
// получает сидовый дефолт localhost:1545; на апгрейде поверх БД, где строка уже есть
// с пустым ValueText, heal проставляет тот же дефолт (иначе публикация падает «Не
// задан адрес 1С-кластера»). Непустые значения и чужие ключи не трогаются.
public sealed class SettingsSeederTests
{
    private const string RasDefault = "localhost:1545";
    private const string AgentPortDefault = "1540";

    [Fact]
    public async Task Fresh_db_seeds_ras_endpoint_with_default()
    {
        using var harness = SeederHarness.Create();

        await harness.SeedAsync();

        var entry = await harness.GetAsync(SettingKey.OneCRasEndpoint);
        entry.Should().NotBeNull();
        entry!.ValueText.Should().Be(RasDefault);
        entry.Value.Should().BeNull();
    }

    [Fact]
    public async Task Heal_fills_empty_existing_ras_endpoint_row()
    {
        using var harness = SeederHarness.Create();

        // Апгрейд: строка уже есть (засеяна до появления дефолта) с ValueText=null.
        await harness.SeedRawAsync(new SettingEntry
        {
            Key = SettingKey.OneCRasEndpoint,
            IsSecret = false,
            ValueText = null,
            Value = null,
            UpdatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            UpdatedBy = "System",
        });

        await harness.SeedAsync();

        var entry = await harness.GetAsync(SettingKey.OneCRasEndpoint);
        entry!.ValueText.Should().Be(RasDefault);
    }

    [Theory]
    [InlineData("myhost:1545")]
    [InlineData("myhost")]
    public async Task Heal_does_not_overwrite_non_empty_ras_endpoint(string existingValue)
    {
        using var harness = SeederHarness.Create();
        await harness.SeedRawAsync(new SettingEntry
        {
            Key = SettingKey.OneCRasEndpoint,
            IsSecret = false,
            ValueText = existingValue,
            Value = null,
            UpdatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            UpdatedBy = "operator",
        });

        await harness.SeedAsync();

        var entry = await harness.GetAsync(SettingKey.OneCRasEndpoint);
        entry!.ValueText.Should().Be(existingValue);
        entry.UpdatedBy.Should().Be("operator", "heal не трогает уже заданное значение");
    }

    [Fact]
    public async Task Fresh_db_seeds_ras_agent_port_with_default()
    {
        using var harness = SeederHarness.Create();

        await harness.SeedAsync();

        var entry = await harness.GetAsync(SettingKey.OneCRasAgentPort);
        entry.Should().NotBeNull();
        entry!.ValueText.Should().Be(AgentPortDefault);
        entry.Value.Should().BeNull();
    }

    [Fact]
    public async Task Heal_fills_empty_existing_ras_agent_port_row()
    {
        using var harness = SeederHarness.Create();

        // Апгрейд: строка ключа существует с пустым ValueText (например, после ручного
        // очищения) — heal проставляет дефолт, иначе адрес агента собирается без порта.
        await harness.SeedRawAsync(new SettingEntry
        {
            Key = SettingKey.OneCRasAgentPort,
            IsSecret = false,
            ValueText = null,
            Value = null,
            UpdatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            UpdatedBy = "System",
        });

        await harness.SeedAsync();

        var entry = await harness.GetAsync(SettingKey.OneCRasAgentPort);
        entry!.ValueText.Should().Be(AgentPortDefault);
    }

    [Fact]
    public async Task Heal_does_not_overwrite_non_empty_ras_agent_port()
    {
        using var harness = SeederHarness.Create();
        await harness.SeedRawAsync(new SettingEntry
        {
            Key = SettingKey.OneCRasAgentPort,
            IsSecret = false,
            ValueText = "1541",
            Value = null,
            UpdatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            UpdatedBy = "operator",
        });

        await harness.SeedAsync();

        var entry = await harness.GetAsync(SettingKey.OneCRasAgentPort);
        entry!.ValueText.Should().Be("1541");
        entry.UpdatedBy.Should().Be("operator", "heal не трогает уже заданное значение");
    }

    [Fact]
    public async Task Seeder_is_idempotent_and_leaves_other_keys_untouched()
    {
        using var harness = SeederHarness.Create();

        await harness.SeedAsync();
        var countAfterFirst = await harness.CountAsync();
        var sqlServerAfterFirst = await harness.GetAsync(SettingKey.SqlServer);

        // Оператор задал чужой ключ между прогонами — повторный сидер его не трогает.
        await harness.SetAsync(SettingKey.SqlServer, "sql.local");

        await harness.SeedAsync();
        var countAfterSecond = await harness.CountAsync();

        countAfterSecond.Should().Be(countAfterFirst, "повторный сидер не плодит дубликаты");
        sqlServerAfterFirst!.ValueText.Should().BeNull("Sql.Server без сидового дефолта");

        var sqlServer = await harness.GetAsync(SettingKey.SqlServer);
        sqlServer!.ValueText.Should().Be("sql.local", "чужой ключ остаётся как задан оператором");

        var ras = await harness.GetAsync(SettingKey.OneCRasEndpoint);
        ras!.ValueText.Should().Be(RasDefault);
    }

    // Поднимает реальный AppDbContext поверх SQLite-in-memory (паттерн TestHelpers.SqliteTestDb)
    // и регистрирует его в DI, чтобы CreateScope().GetRequiredService<AppDbContext>() внутри
    // сидера получил контекст над тем же соединением (общая БД на время теста).
    private sealed class SeederHarness : IDisposable
    {
        private readonly SqliteConnection _connection;
        private readonly ServiceProvider _provider;
        private readonly DbContextOptions<AppDbContext> _options;

        private SeederHarness(SqliteConnection connection, ServiceProvider provider, DbContextOptions<AppDbContext> options)
        {
            _connection = connection;
            _provider = provider;
            _options = options;
        }

        public static SeederHarness Create()
        {
            var connection = new SqliteConnection("DataSource=:memory:;Foreign Keys=True");
            connection.Open();

            var services = new ServiceCollection();
            services.AddSingleton(TimeProvider.System);
            services.AddSingleton<ILoggerFactory>(NullLoggerFactory.Instance);
            services.AddDbContext<AppDbContext>(o => o
                .UseSqlite(connection)
                .ReplaceService<IModelCustomizer, VarbinaryToBlobModelCustomizer>());

            var provider = services.BuildServiceProvider();

            using (var scope = provider.CreateScope())
            {
                var options = scope.ServiceProvider.GetRequiredService<AppDbContext>().Database;
                options.EnsureCreated();
            }

            var dbOptions = new DbContextOptionsBuilder<AppDbContext>()
                .UseSqlite(connection)
                .ReplaceService<IModelCustomizer, VarbinaryToBlobModelCustomizer>()
                .Options;

            return new SeederHarness(connection, provider, dbOptions);
        }

        public Task SeedAsync() => SettingsSeeder.EnsureSeededAsync(_provider);

        public async Task SeedRawAsync(SettingEntry entry)
        {
            await using var ctx = new AppDbContext(_options);
            ctx.Settings.Add(entry);
            await ctx.SaveChangesAsync();
        }

        public async Task SetAsync(string key, string value)
        {
            await using var ctx = new AppDbContext(_options);
            var entry = await ctx.Settings.SingleAsync(s => s.Key == key);
            entry.ValueText = value;
            entry.UpdatedBy = "operator";
            await ctx.SaveChangesAsync();
        }

        public async Task<SettingEntry?> GetAsync(string key)
        {
            await using var ctx = new AppDbContext(_options);
            return await ctx.Settings.AsNoTracking().SingleOrDefaultAsync(s => s.Key == key);
        }

        public async Task<int> CountAsync()
        {
            await using var ctx = new AppDbContext(_options);
            return await ctx.Settings.CountAsync();
        }

        public void Dispose()
        {
            _provider.Dispose();
            _connection.Dispose();
        }
    }

    // dbo.Settings.Value — varbinary(max) (DPAPI payload), SQLite его DDL не парсит:
    // переписываем в нативный BLOB только для тестового провайдера (как в TestHelpers).
    private sealed class VarbinaryToBlobModelCustomizer : RelationalModelCustomizer
    {
        public VarbinaryToBlobModelCustomizer(ModelCustomizerDependencies dependencies)
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
                    columnType.Contains("varbinary", StringComparison.OrdinalIgnoreCase))
                {
                    property.SetColumnType("BLOB");
                }
            }
        }
    }
}
