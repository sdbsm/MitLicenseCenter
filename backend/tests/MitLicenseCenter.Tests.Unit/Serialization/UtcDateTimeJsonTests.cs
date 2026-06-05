using System.Text.Json;
using System.Text.Json.Serialization;
using FluentAssertions;
using Xunit;

namespace MitLicenseCenter.Tests.Unit.Serialization;

// Контракт для frontend: DateTime уходит в JSON с суффиксом `Z` (UTC-инстант).
// System.Text.Json печатает `Z` только для Kind=Utc; для Unspecified — голую строку
// без зоны, которую браузерный new Date() трактует как локальное время. Поэтому из
// БД (datetime2 → Kind=Unspecified) метку нормализуем к Utc (AppDbContext
// ConfigureConventions). Эти тесты фиксируют ОБЕ половины контракта.
public sealed class UtcDateTimeJsonTests
{
    private static readonly JsonSerializerOptions Options = BuildOptionsLikeWeb();

    private sealed record Probe(DateTime Stamp, DateTime? StampNullable);

    [Fact]
    public void Utc_DateTime_serializes_with_Z_suffix()
    {
        var probe = new Probe(
            new DateTime(2026, 6, 5, 9, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 6, 5, 9, 0, 0, DateTimeKind.Utc));

        var json = JsonSerializer.Serialize(probe, Options);

        json.Should().Contain("\"stamp\":\"2026-06-05T09:00:00Z\"");
        json.Should().Contain("\"stampNullable\":\"2026-06-05T09:00:00Z\"");
    }

    [Fact]
    public void Unspecified_DateTime_serializes_without_Z_documenting_the_bug()
    {
        // Anti-contract: именно так выглядела бы метка без UTC-нормализации —
        // без `Z`, поэтому фронт парсил её как локальное время и сдвигал.
        var probe = new Probe(
            new DateTime(2026, 6, 5, 9, 0, 0, DateTimeKind.Unspecified),
            null);

        var json = JsonSerializer.Serialize(probe, Options);

        json.Should().Contain("\"stamp\":\"2026-06-05T09:00:00\"");
        json.Should().NotContain("Z\"");
    }

    private static JsonSerializerOptions BuildOptionsLikeWeb()
    {
        // Зеркало настроек из Program.cs (camelCase + строковые enum'ы).
        var opts = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        };
        opts.Converters.Add(new JsonStringEnumConverter());
        return opts;
    }
}
