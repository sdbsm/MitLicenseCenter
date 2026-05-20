using FluentAssertions;
using MitLicenseCenter.Application.Publishing;
using MitLicenseCenter.Domain.Publications;
using MitLicenseCenter.Infrastructure.Publishing;
using Xunit;

namespace MitLicenseCenter.Tests.Unit.Publishing;

// Pure compare-функция: подаём (desired, actual) → ожидаем статус + details.
// Без файловой системы и IIS — это контракт PR 3.5: detector не должен
// требовать никакого I/O.
public sealed class PublicationDriftDetectorTests
{
    private static Publication Desired(
        bool odata = true,
        bool http = true,
        string version = "8.3.23.1865") => new()
        {
            Id = Guid.NewGuid(),
            InfobaseId = Guid.NewGuid(),
            SiteName = "Default Web Site",
            VirtualPath = "/MyPub",
            PlatformVersion = version,
            EnableOData = odata,
            EnableHttpServices = http,
            CreatedAt = DateTime.UtcNow,
        };

    private static PublicationActualState Actual(
        bool siteExists = true,
        bool vpathExists = true,
        string? platformVersion = "8.3.23.1865",
        bool odata = true,
        bool http = true,
        string? vrdContent = "<point/>",
        string? error = null) => new(
            SiteExists: siteExists,
            VirtualPathExists: vpathExists,
            PlatformVersion: platformVersion,
            EnableOData: odata,
            EnableHttpServices: http,
            VrdContent: vrdContent,
            Error: error);

    [Fact]
    public void InSync_when_all_fields_match()
    {
        var (status, details) = PublicationDriftDetector.Compare(Desired(), Actual());

        status.Should().Be(PublicationDriftStatus.InSync);
        details.Should().BeEmpty();
    }

    [Fact]
    public void Drift_on_OData_mismatch()
    {
        var (status, details) = PublicationDriftDetector.Compare(
            Desired(odata: true),
            Actual(odata: false));

        status.Should().Be(PublicationDriftStatus.Drift);
        details.Should().Contain("OData");
    }

    [Fact]
    public void Drift_on_HttpServices_mismatch()
    {
        var (status, details) = PublicationDriftDetector.Compare(
            Desired(http: true),
            Actual(http: false));

        status.Should().Be(PublicationDriftStatus.Drift);
        details.Should().Contain("HTTP");
    }

    [Fact]
    public void Drift_on_PlatformVersion_mismatch()
    {
        var (status, details) = PublicationDriftDetector.Compare(
            Desired(version: "8.3.26.1521"),
            Actual(platformVersion: "8.3.22.1709"));

        status.Should().Be(PublicationDriftStatus.Drift);
        details.Should().Contain("8.3.26.1521");
        details.Should().Contain("8.3.22.1709");
    }

    [Fact]
    public void Missing_when_site_absent()
    {
        var (status, details) = PublicationDriftDetector.Compare(
            Desired(),
            Actual(siteExists: false));

        status.Should().Be(PublicationDriftStatus.Missing);
        details.Should().Contain("сайт");
    }

    [Fact]
    public void Missing_when_virtual_path_absent()
    {
        var (status, details) = PublicationDriftDetector.Compare(
            Desired(),
            Actual(vpathExists: false));

        status.Should().Be(PublicationDriftStatus.Missing);
        details.Should().Contain("Виртуальный путь");
    }

    [Fact]
    public void Missing_when_vrd_file_absent()
    {
        var (status, details) = PublicationDriftDetector.Compare(
            Desired(),
            Actual(vrdContent: null));

        status.Should().Be(PublicationDriftStatus.Missing);
        details.Should().Contain("default.vrd");
    }

    [Fact]
    public void Error_when_adapter_reports_error()
    {
        var (status, details) = PublicationDriftDetector.Compare(
            Desired(),
            Actual(error: "Access denied: IIS Metabase"));

        status.Should().Be(PublicationDriftStatus.Error);
        details.Should().Contain("Access denied");
    }

    [Fact]
    public void Drift_combines_multiple_diffs_into_one_details()
    {
        var (status, details) = PublicationDriftDetector.Compare(
            Desired(odata: true, http: true, version: "8.3.26.1521"),
            Actual(odata: false, http: false, platformVersion: "8.3.22.1709"));

        status.Should().Be(PublicationDriftStatus.Drift);
        details.Should().Contain("OData");
        details.Should().Contain("HTTP");
        details.Should().Contain("8.3.26.1521");
    }
}
