using FluentAssertions;
using FluentAssertions.Execution;
using Microsoft.EntityFrameworkCore;
using MitLicenseCenter.Infrastructure.Persistence;
using Xunit;

namespace MitLicenseCenter.Tests.Unit.Persistence;

// UTC на проводе: все DateTime/DateTime? свойства, читаемые из datetime2-колонок,
// должны материализоваться с Kind=Utc, иначе System.Text.Json отдаёт строку без `Z`
// и браузер парсит её как локальное время (метка уезжает на величину часового пояса).
// Конвенция живёт в AppDbContext.ConfigureConventions; тест стережёт, чтобы её не
// сняли и чтобы она покрывала каждое DateTime-свойство модели.
public sealed class UtcDateTimeConventionTests
{
    private static AppDbContext NewContext() =>
        new(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"utc-convention-{Guid.NewGuid():N}")
            .Options);

    [Fact]
    public void Every_DateTime_property_reads_back_as_Utc()
    {
        using var ctx = NewContext();

        var dateTimeProps = ctx.Model.GetEntityTypes()
            .SelectMany(e => e.GetProperties())
            .Where(p => p.ClrType == typeof(DateTime) || p.ClrType == typeof(DateTime?))
            .ToList();

        dateTimeProps.Should().NotBeEmpty(
            "в модели есть datetime2-колонки (CreatedAt/UpdatedAt/LastCheckAt/Timestamp)");

        // Имитируем чтение из БД: provider отдаёт значение без зоны (Unspecified).
        var fromDb = new DateTime(2026, 6, 5, 9, 0, 0, DateTimeKind.Unspecified);

        using var scope = new AssertionScope();
        foreach (var prop in dateTimeProps)
        {
            var converter = prop.GetValueConverter();
            var owner = $"{prop.DeclaringType.ClrType.Name}.{prop.Name}";

            converter.Should().NotBeNull($"{owner} должно нести UTC-конвертер");

            var materialized = (DateTime)converter!.ConvertFromProvider(fromDb)!;
            materialized.Kind.Should().Be(DateTimeKind.Utc, $"{owner} читается как UTC");
        }
    }
}
