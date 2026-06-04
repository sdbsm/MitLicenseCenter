using FluentAssertions;
using MitLicenseCenter.Infrastructure.Diagnostics;
using Xunit;

namespace MitLicenseCenter.Tests.Unit.Diagnostics;

// MLC-037 (PERF-01): тег команды rac.exe выводится из её аргументов в той же форме,
// что строит RacExecutableRasClusterClient (BuildArgs/BuildArgsWithAuth): опциональный
// endpoint-токен в начале + глагол/подглагол + --опции. Проверяем все четыре реальные
// команды (с endpoint и без, с auth-флагами) и неизвестную форму.
public sealed class RacCommandTagTests
{
    private const string Uuid = "613f185a-339d-4bc5-88ad-16acd14a4d26";
    private const string Session = "492af167-20e6-497a-9eef-20ce4e930c6a";

    [Fact]
    public void Cluster_list_without_endpoint()
        => RacCommandTag.For(["cluster", "list"]).Should().Be(RacCommandTag.ClusterList);

    [Fact]
    public void Cluster_list_with_endpoint()
        => RacCommandTag.For(["localhost:1545", "cluster", "list"]).Should().Be(RacCommandTag.ClusterList);

    [Fact]
    public void Session_list_with_endpoint_and_auth_is_not_confused_by_cluster_option()
    {
        // --cluster=<uuid> присутствует, но это опция, а не глагол: ожидаем session.list.
        var args = new[]
        {
            "localhost:1545", "session", "list",
            $"--cluster={Uuid}", "--cluster-user=admin", "--cluster-pwd=secret",
        };

        RacCommandTag.For(args).Should().Be(RacCommandTag.SessionList);
    }

    [Fact]
    public void Session_terminate()
    {
        var args = new[]
        {
            "localhost:1545", "session", "terminate",
            $"--cluster={Uuid}", $"--session={Session}",
        };

        RacCommandTag.For(args).Should().Be(RacCommandTag.SessionTerminate);
    }

    [Fact]
    public void Infobase_summary_list()
    {
        var args = new[] { "localhost:1545", "infobase", "summary", "list", $"--cluster={Uuid}" };

        RacCommandTag.For(args).Should().Be(RacCommandTag.InfobaseSummaryList);
    }

    [Fact]
    public void Unknown_form_falls_back_to_other()
        => RacCommandTag.For(["foo", "bar"]).Should().Be(RacCommandTag.Other);

    [Fact]
    public void Empty_args_fall_back_to_other()
        => RacCommandTag.For([]).Should().Be(RacCommandTag.Other);
}
