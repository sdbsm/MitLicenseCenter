using FluentAssertions;
using MitLicenseCenter.Application.Publishing;
using MitLicenseCenter.Domain.Publications;
using MitLicenseCenter.Infrastructure.Publishing;
using Xunit;

namespace MitLicenseCenter.Tests.Unit.Publishing;

// MLC-045: pure-хелпер read-only оценки статуса публикации (без сравнения с эталоном).
public sealed class PublicationStatusEvaluatorTests
{
    private static Publication Pub() => new()
    {
        Id = Guid.NewGuid(),
        SiteName = "Default Web Site",
        VirtualPath = "/acme",
        PlatformVersion = "8.3.23.1865",
    };

    [Fact]
    public void Error_beats_everything()
    {
        var actual = new PublicationActualState(true, true, true, "8.3.23.1865", Error: "COM 0x80070005");
        var (status, details) = PublicationStatusEvaluator.Evaluate(Pub(), actual);

        status.Should().Be(PublicationPublishStatus.Error);
        details.Should().Contain("COM 0x80070005");
    }

    [Fact]
    public void NotPublished_when_site_missing()
    {
        var actual = new PublicationActualState(false, false, false, null, null);
        PublicationStatusEvaluator.Evaluate(Pub(), actual).Status.Should().Be(PublicationPublishStatus.NotPublished);
    }

    [Fact]
    public void NotPublished_when_virtual_path_missing()
    {
        var actual = new PublicationActualState(true, false, false, null, null);
        PublicationStatusEvaluator.Evaluate(Pub(), actual).Status.Should().Be(PublicationPublishStatus.NotPublished);
    }

    [Fact]
    public void NotPublished_when_web_config_missing()
    {
        var actual = new PublicationActualState(true, true, false, null, null);
        PublicationStatusEvaluator.Evaluate(Pub(), actual).Status.Should().Be(PublicationPublishStatus.NotPublished);
    }

    [Fact]
    public void Published_when_all_present_and_reports_version()
    {
        var actual = new PublicationActualState(true, true, true, "8.3.24.1234", null);
        var (status, details) = PublicationStatusEvaluator.Evaluate(Pub(), actual);

        status.Should().Be(PublicationPublishStatus.Published);
        details.Should().Contain("8.3.24.1234");
    }
}
