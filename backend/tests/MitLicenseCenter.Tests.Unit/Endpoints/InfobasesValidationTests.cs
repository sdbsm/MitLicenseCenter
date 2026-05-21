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
            EnableOData: false,
            EnableHttpServices: false,
            VrdCustomXml: null);

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
        InfobasesEndpoints.IsValidPlatformVersion(value).Should().Be(expected);
    }
}
