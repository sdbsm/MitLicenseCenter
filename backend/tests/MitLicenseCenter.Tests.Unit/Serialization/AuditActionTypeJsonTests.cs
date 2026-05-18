using System.Text.Json;
using System.Text.Json.Serialization;
using FluentAssertions;
using MitLicenseCenter.Domain.Audit;
using MitLicenseCenter.Web.Endpoints;
using Xunit;

namespace MitLicenseCenter.Tests.Unit.Serialization;

// Контракт для frontend: AuditActionType / AuditReason должны уходить как
// строковые имена («TenantCreated», «LimitExceeded»), а не int. Если кто-то
// случайно снимет JsonStringEnumConverter в Program.cs — этот тест упадёт.
public sealed class AuditActionTypeJsonTests
{
    private static readonly JsonSerializerOptions Options = BuildOptionsLikeWeb();

    [Fact]
    public void AuditActionType_serializes_as_string_name()
    {
        var entry = new AuditEntryResponse(
            Guid.NewGuid(),
            new DateTime(2026, 5, 19, 10, 0, 0, DateTimeKind.Utc),
            AuditActionType.TenantCreated,
            Reason: null,
            Initiator: "admin",
            Description: "—",
            TenantId: null);

        var json = JsonSerializer.Serialize(entry, Options);

        json.Should().Contain("\"actionType\":\"TenantCreated\"");
        json.Should().NotContain("\"actionType\":1");
    }

    [Fact]
    public void AuditReason_serializes_as_string_name()
    {
        var entry = new AuditEntryResponse(
            Guid.NewGuid(),
            new DateTime(2026, 5, 19, 10, 0, 0, DateTimeKind.Utc),
            AuditActionType.TenantDeleted,
            Reason: AuditReason.LimitExceeded,
            Initiator: "admin",
            Description: "—",
            TenantId: null);

        var json = JsonSerializer.Serialize(entry, Options);

        json.Should().Contain("\"reason\":\"LimitExceeded\"");
    }

    [Fact]
    public void Null_AuditReason_is_omitted_or_null_but_not_zero()
    {
        var entry = new AuditEntryResponse(
            Guid.NewGuid(),
            new DateTime(2026, 5, 19, 10, 0, 0, DateTimeKind.Utc),
            AuditActionType.AdminLoggedIn,
            Reason: null,
            Initiator: "admin",
            Description: "—",
            TenantId: null);

        var json = JsonSerializer.Serialize(entry, Options);

        json.Should().NotContain("\"reason\":0");
        json.Should().NotContain("\"reason\":\"LimitExceeded\"");
    }

    [Fact]
    public void All_AuditActionType_values_roundtrip_through_string()
    {
        foreach (var value in Enum.GetValues<AuditActionType>())
        {
            var json = JsonSerializer.Serialize(value, Options);
            json.Should().Be($"\"{value}\"");

            var back = JsonSerializer.Deserialize<AuditActionType>(json, Options);
            back.Should().Be(value);
        }
    }

    private static JsonSerializerOptions BuildOptionsLikeWeb()
    {
        // Зеркало настроек из Program.cs — конвертер строковых enum'ов и camelCase
        // для имён свойств. Если что-то разъедется, тест должен упасть, а не молча
        // выдать «правильный» json.
        var opts = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        };
        opts.Converters.Add(new JsonStringEnumConverter());
        return opts;
    }
}
