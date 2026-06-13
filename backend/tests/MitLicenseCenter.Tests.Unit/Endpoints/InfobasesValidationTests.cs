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
        InfobaseValidationRules.DatabaseNameMaxLength.Should().Be(200);
        InfobaseValidationRules.SiteNameMaxLength.Should().Be(200);
        InfobaseValidationRules.VirtualPathMaxLength.Should().Be(200);
        InfobaseValidationRules.PlatformVersionMaxLength.Should().Be(50);
        InfobaseValidationRules.PhysicalPathMaxLength.Should().Be(260);
    }

    // MLC-118 — golden-таблицы барьера валидации. Парные FE-таблицам в validation.test.ts;
    // проза-спека — docs/03_DOMAIN_MODEL.md (§1.1, §3.5). Бьют по единому источнику
    // (InfobaseValidationRules.AppendInfobaseFieldErrors / AppendPublicationFieldErrors),
    // из которого реально валидируют рантайм-эндпоинты (DataAnnotations в minimal API не
    // срабатывают — гоча CLAUDE.md).

    private static Dictionary<string, string[]> ValidateInfobaseFields(string name, string databaseName)
    {
        var errors = new Dictionary<string, string[]>(StringComparer.Ordinal);
        InfobaseValidationRules.AppendInfobaseFieldErrors(errors, "Name", "DatabaseName", name, databaseName);
        return errors;
    }

    private static Dictionary<string, string[]> ValidatePublicationFields(
        string siteName = "Default Web Site",
        string virtualPath = "/acme-bp",
        string platformVersion = "8.3.23.1865",
        string? physicalPathOverride = null)
    {
        var errors = new Dictionary<string, string[]>(StringComparer.Ordinal);
        InfobaseValidationRules.AppendPublicationFieldErrors(
            errors, string.Empty, siteName, virtualPath, platformVersion, physicalPathOverride);
        return errors;
    }

    // BE-07 / SEC-13 — Infobase.Name connstr-safe: запрет «; = "» и control-символов.
    [Theory]
    [InlineData("Бухгалтерия", true)]
    [InlineData("Acme BP", true)]
    [InlineData("acme-bp_01", true)]
    [InlineData("Acme;Ref=evil", false)]
    [InlineData("Acme=evil", false)]
    [InlineData("Acme\"quote", false)]
    [InlineData("Acme\tTab", false)]
    public void Name_rejects_connstr_metachars(string name, bool valid)
    {
        var errors = ValidateInfobaseFields(name, "acme_bp");
        errors.ContainsKey("Name").Should().Be(!valid);
    }

    [Fact]
    public void Name_too_long_is_rejected()
    {
        var name = new string('a', InfobaseValidationRules.NameMaxLength + 1);
        ValidateInfobaseFields(name, "acme_bp").Should().ContainKey("Name");
    }

    [Fact]
    public void Name_at_max_length_passes()
    {
        var name = new string('a', InfobaseValidationRules.NameMaxLength);
        ValidateInfobaseFields(name, "acme_bp").Should().NotContainKey("Name");
    }

    // SEC-12 / UX-11 — DatabaseName: запрет path/служебных метасимволов и «..».
    [Theory]
    [InlineData("acme_bp", true)]
    [InlineData("Acme.BP", true)]
    [InlineData("acme bp", true)]
    [InlineData("..\\evil", false)]
    [InlineData("a..b", false)]
    [InlineData("a/b", false)]
    [InlineData("a\\b", false)]
    [InlineData("a:b", false)]
    [InlineData("a*b", false)]
    [InlineData("a?b", false)]
    [InlineData("a\"b", false)]
    [InlineData("a<b", false)]
    [InlineData("a>b", false)]
    [InlineData("a|b", false)]
    [InlineData("a;b", false)]
    [InlineData("a'b", false)]
    [InlineData("a[b]", false)]
    [InlineData("a\tb", false)]
    public void DatabaseName_rejects_path_and_meta_chars(string databaseName, bool valid)
    {
        var errors = ValidateInfobaseFields("Acme", databaseName);
        errors.ContainsKey("DatabaseName").Should().Be(!valid);
    }

    [Fact]
    public void DatabaseName_too_long_is_rejected()
    {
        var db = new string('a', InfobaseValidationRules.DatabaseNameMaxLength + 1);
        ValidateInfobaseFields("Acme", db).Should().ContainKey("DatabaseName");
    }

    // SEC-11 — VirtualPath: в дополнение к «/»-старту и пробелам — запрет «\», «..», control.
    [Theory]
    [InlineData("/acme-bp", true)]
    [InlineData("/acme/sub", true)]
    [InlineData("/a..b", false)]
    [InlineData("/a\\b", false)]
    [InlineData("/a\tb", false)]
    public void VirtualPath_rejects_traversal_and_backslash(string virtualPath, bool valid)
    {
        var errors = ValidatePublicationFields(virtualPath: virtualPath);
        errors.ContainsKey("VirtualPath").Should().Be(!valid);
    }

    [Fact]
    public void VirtualPath_too_long_is_rejected()
    {
        var vp = "/" + new string('a', InfobaseValidationRules.VirtualPathMaxLength);
        ValidatePublicationFields(virtualPath: vp).Should().ContainKey("VirtualPath");
    }

    [Fact]
    public void PlatformVersion_too_long_is_rejected()
    {
        var version = "8.3.23." + new string('1', InfobaseValidationRules.PlatformVersionMaxLength);
        ValidatePublicationFields(platformVersion: version).Should().ContainKey("PlatformVersion");
    }

    // SEC-11 — PhysicalPathOverride: абсолютный путь + длина + запрет «..», «; = "», control.
    [Theory]
    [InlineData(@"C:\pub\app", true)]
    [InlineData(@"\\server\share\app", true)]
    [InlineData(@"D:\inetpub\wwwroot\acme_bp", true)]
    [InlineData(@"relative\path", false)]      // не абсолютный
    [InlineData(@"C:\pub\..\app", false)]       // traversal
    [InlineData("C:\\pub;evil", false)]          // «;»
    [InlineData("C:\\pub=evil", false)]          // «=»
    [InlineData("C:\\pub\"q", false)]            // «"»
    [InlineData("C:\\pub\tx", false)]            // control
    public void PhysicalPathOverride_rules(string physicalPath, bool valid)
    {
        var errors = ValidatePublicationFields(physicalPathOverride: physicalPath);
        errors.ContainsKey("PhysicalPathOverride").Should().Be(!valid);
    }

    [Fact]
    public void PhysicalPathOverride_too_long_is_rejected()
    {
        var pp = @"C:\" + new string('a', InfobaseValidationRules.PhysicalPathMaxLength);
        ValidatePublicationFields(physicalPathOverride: pp).Should().ContainKey("PhysicalPathOverride");
    }

    [Fact]
    public void All_valid_fields_produce_no_errors()
    {
        ValidateInfobaseFields("Бухгалтерия", "acme_bp").Should().BeEmpty();
        ValidatePublicationFields(physicalPathOverride: @"C:\pub\acme").Should().BeEmpty();
    }

    // Предикаты безопасности символов — пины набора (парные FE-предикатам в validation.ts).
    [Theory]
    [InlineData("Acme", true)]
    [InlineData("Acme;x", false)]
    [InlineData("Acme=x", false)]
    [InlineData("Acme\"x", false)]
    public void IsConnStrSafeName_pins(string value, bool expected) =>
        InfobaseValidationRules.IsConnStrSafeName(value).Should().Be(expected);

    [Theory]
    [InlineData("acme_bp", true)]
    [InlineData("a..b", false)]
    [InlineData("a/b", false)]
    [InlineData("a[b]", false)]
    public void IsSafeDatabaseName_pins(string value, bool expected) =>
        InfobaseValidationRules.IsSafeDatabaseName(value).Should().Be(expected);

    [Theory]
    [InlineData("/acme", true)]
    [InlineData("/a..b", false)]
    [InlineData("/a\\b", false)]
    public void IsSafeVirtualPath_pins(string value, bool expected) =>
        InfobaseValidationRules.IsSafeVirtualPath(value).Should().Be(expected);

    [Theory]
    [InlineData(@"C:\pub\app", true)]
    [InlineData(@"C:\pub\..\app", false)]
    [InlineData("C:\\pub;x", false)]
    public void IsSafePhysicalPath_pins(string value, bool expected) =>
        InfobaseValidationRules.IsSafePhysicalPath(value).Should().Be(expected);
}
