using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using MitLicenseCenter.Infrastructure.Identity;
using MitLicenseCenter.Infrastructure.Persistence;
using Xunit;

namespace MitLicenseCenter.Tests.Unit;

public sealed class SmokeTests
{
    [Fact]
    public void Roles_constants_must_match_expected_strings()
    {
        Roles.Admin.Should().Be("Admin");
        Roles.Viewer.Should().Be("Viewer");
        Roles.All.Should().BeEquivalentTo([Roles.Admin, Roles.Viewer]);
    }

    [Fact]
    public void AppDbContext_exposes_all_expected_DbSets()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"smoke-{Guid.NewGuid():N}")
            .Options;

        using var db = new AppDbContext(options);

        db.Tenants.Should().NotBeNull();
        db.AuditLogs.Should().NotBeNull();
        db.Users.Should().NotBeNull();
        db.Roles.Should().NotBeNull();
        db.UserRoles.Should().NotBeNull();
    }
}
