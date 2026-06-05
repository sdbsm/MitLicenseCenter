using System.ComponentModel.DataAnnotations;
using FluentAssertions;
using MitLicenseCenter.Domain.Infobases;
using MitLicenseCenter.Web.Endpoints;
using Xunit;

namespace MitLicenseCenter.Tests.Unit.Endpoints;

public sealed class InfobasesValidationTests
{
    private static CreatePublicationRequest ValidPublication() =>
        new(
            SiteName: "Default Web Site",
            VirtualPath: "/acme-bp",
            PlatformVersion: "8.3.23.1865",
            PhysicalPathOverride: null);

    [Fact]
    public void CreateInfobaseRequest_with_valid_input_passes_DataAnnotations()
    {
        var request = new CreateInfobaseRequest(
            TenantId: Guid.NewGuid(),
            Name: "Бухгалтерия",
            ClusterInfobaseId: Guid.NewGuid(),
            DatabaseServer: "sql.local",
            DatabaseName: "acme_bp",
            Status: InfobaseStatus.Active,
            Publication: ValidPublication());

        var ctx = new ValidationContext(request);
        var results = new List<ValidationResult>();

        Validator.TryValidateObject(request, ctx, results, validateAllProperties: true).Should().BeTrue();
    }

    [Fact]
    public void CreateInfobaseRequest_with_empty_name_fails()
    {
        var request = new CreateInfobaseRequest(
            TenantId: Guid.NewGuid(),
            Name: string.Empty,
            ClusterInfobaseId: Guid.NewGuid(),
            DatabaseServer: "sql.local",
            DatabaseName: "acme_bp",
            Status: InfobaseStatus.Active,
            Publication: ValidPublication());

        var ctx = new ValidationContext(request);
        var results = new List<ValidationResult>();

        Validator.TryValidateObject(request, ctx, results, validateAllProperties: true).Should().BeFalse();
        results.Should().Contain(r => r.MemberNames.Contains(nameof(CreateInfobaseRequest.Name)));
    }

    // MLC-022 — golden-таблица версии платформы. Идентична FE-таблице в
    // frontend/src/features/infobases/__tests__/validation.test.ts: обе пинятся к
    // прозе-спеке docs/03_DOMAIN_MODEL.md (§3) и ловят дрейф regex FE↔BE без codegen.
    [Theory]
    [InlineData("8.3.23.1865", true)]
    [InlineData("8.3.24.1654", true)]
    [InlineData("8.5.1.1302", true)]   // 1С 8.5 ранние сборки — build одноцифровой
    [InlineData("8.3.1.1865", true)]   // build одноцифровой — допустимо
    [InlineData("8.3.23.18", true)]    // короткая revision — допустимо
    [InlineData("10.0.10.0001", true)]
    [InlineData("8.3", false)]
    [InlineData("8.3.23", false)]
    [InlineData("8.3.23.", false)]
    [InlineData("", false)]
    [InlineData("a.b.c.d", false)]
    [InlineData("8.3.23.1865.0", false)]
    public void PlatformVersion_regex_requires_four_numeric_segments(string value, bool expected)
    {
        InfobaseValidationRules.IsValidPlatformVersion(value).Should().Be(expected);
    }

    // MLC-022 — пины единого источника к литералам спеки 03_DOMAIN_MODEL.md. Любая правка
    // regex/лимита ломает этот тест (и парный FE-тест), пока спека не изменена осознанно.
    [Fact]
    public void Validation_rules_match_documented_spec()
    {
        InfobaseValidationRules.PlatformVersionRegex().ToString().Should().Be(@"^\d+\.\d+\.\d+\.\d+$");
        InfobaseValidationRules.NameMaxLength.Should().Be(200);
        InfobaseValidationRules.DatabaseServerMaxLength.Should().Be(200);
        InfobaseValidationRules.DatabaseNameMaxLength.Should().Be(200);
        InfobaseValidationRules.SiteNameMaxLength.Should().Be(200);
        InfobaseValidationRules.VirtualPathMaxLength.Should().Be(200);
        InfobaseValidationRules.PlatformVersionMaxLength.Should().Be(50);
        InfobaseValidationRules.PhysicalPathMaxLength.Should().Be(260);
    }
}
