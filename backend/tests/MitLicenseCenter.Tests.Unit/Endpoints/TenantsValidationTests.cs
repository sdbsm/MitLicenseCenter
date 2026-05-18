using System.ComponentModel.DataAnnotations;
using FluentAssertions;
using MitLicenseCenter.Web.Endpoints;
using Xunit;

namespace MitLicenseCenter.Tests.Unit.Endpoints;

public sealed class TenantsValidationTests
{
    [Fact]
    public void CreateTenantRequest_with_empty_name_and_negative_limit_yields_two_errors()
    {
        var request = new CreateTenantRequest(Name: string.Empty, MaxConcurrentLicenses: -1, IsActive: true);
        var ctx = new ValidationContext(request);
        var results = new List<ValidationResult>();

        var ok = Validator.TryValidateObject(request, ctx, results, validateAllProperties: true);

        ok.Should().BeFalse();
        results.Should().HaveCount(2);
        results.SelectMany(r => r.MemberNames).Should().Contain(new[] { nameof(CreateTenantRequest.Name), nameof(CreateTenantRequest.MaxConcurrentLicenses) });
    }

    [Fact]
    public void CreateTenantRequest_with_valid_input_passes()
    {
        var request = new CreateTenantRequest(Name: "Acme Co", MaxConcurrentLicenses: 100, IsActive: true);
        var ctx = new ValidationContext(request);
        var results = new List<ValidationResult>();

        Validator.TryValidateObject(request, ctx, results, validateAllProperties: true).Should().BeTrue();
    }

    [Fact]
    public void CreateTenantRequest_limit_above_max_fails()
    {
        var request = new CreateTenantRequest(Name: "Acme Co", MaxConcurrentLicenses: 100_001, IsActive: true);
        var ctx = new ValidationContext(request);
        var results = new List<ValidationResult>();

        Validator.TryValidateObject(request, ctx, results, validateAllProperties: true).Should().BeFalse();
        results.Should().ContainSingle(r => r.MemberNames.Contains(nameof(CreateTenantRequest.MaxConcurrentLicenses)));
    }
}
