using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using MitLicenseCenter.Application.Clusters;
using MitLicenseCenter.Application.Settings;
using MitLicenseCenter.Domain.Settings;
using MitLicenseCenter.Infrastructure.Clusters;
using NSubstitute;
using Xunit;

namespace MitLicenseCenter.Tests.Unit.Clusters;

// Unit-тесты RAS-адаптера с fake IRacProcessRunner. Реальный rac.exe не запускается —
// это прерогатива RacExecutableSmokeTests.
public sealed class RacExecutableRasClusterClientTests
{
    private const string FakeClusterListStdout =
        "cluster : 613f185a-339d-4bc5-88ad-16acd14a4d26\r\n" +
        "host    : Andrey-pc\r\n" +
        "port    : 1541\r\n" +
        "name    : \"Локальный кластер\"\r\n";

    private const string FakeSingleSessionStdout =
        "session       : 492af167-20e6-497a-9eef-20ce4e930c6a\r\n" +
        "infobase      : 6256b6f3-dde1-41f9-a6c2-bdfc36bca7aa\r\n" +
        "app-id        : 1CV8C\r\n" +
        "user-name     : Андрей\r\n" +
        "host          : workstation01\r\n" +
        "started-at    : 2026-05-21T03:39:49\r\n";

    [Fact]
    public async Task ListActiveSessionsAsync_resolves_cluster_then_lists_sessions_and_maps_fields()
    {
        var settings = BuildSettings();
        var runner = Substitute.For<IRacProcessRunner>();
        runner.RunAsync("rac.exe", Arg.Is<IReadOnlyList<string>>(a => a.Contains("cluster") && a.Contains("list")), Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>())
            .Returns(new RacInvocation(0, FakeClusterListStdout, string.Empty));
        runner.RunAsync("rac.exe", Arg.Is<IReadOnlyList<string>>(a => a.Contains("session") && a.Contains("list")), Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>())
            .Returns(new RacInvocation(0, FakeSingleSessionStdout, string.Empty));

        var client = new RacExecutableRasClusterClient(runner, settings, NullLogger<RacExecutableRasClusterClient>.Instance);

        var sessions = await client.ListActiveSessionsAsync(default);

        sessions.Should().HaveCount(1);
        var s = sessions[0];
        s.SessionId.Should().Be(Guid.Parse("492af167-20e6-497a-9eef-20ce4e930c6a"));
        s.ClusterInfobaseId.Should().Be(Guid.Parse("6256b6f3-dde1-41f9-a6c2-bdfc36bca7aa"));
        s.AppId.Should().Be("1CV8C");
        s.UserName.Should().Be("Андрей");
        s.Host.Should().Be("workstation01");
        s.ConsumesLicense.Should().BeTrue("1CV8C ∈ LicenseConsumingAppIds");
        s.StartedAtUtc.Kind.Should().Be(DateTimeKind.Utc);
    }

    [Fact]
    public async Task ListActiveSessionsAsync_filters_consumes_license_via_app_id_heuristic()
    {
        const string twoSessions =
            "session : 11111111-1111-1111-1111-111111111111\r\n" +
            "infobase : 6256b6f3-dde1-41f9-a6c2-bdfc36bca7aa\r\n" +
            "app-id : 1CV8\r\n" +
            "\r\n" +
            "session : 22222222-2222-2222-2222-222222222222\r\n" +
            "infobase : 6256b6f3-dde1-41f9-a6c2-bdfc36bca7aa\r\n" +
            "app-id : BackgroundJob\r\n";

        var settings = BuildSettings();
        var runner = BuildRunner(clusterList: FakeClusterListStdout, sessionList: twoSessions);
        var client = new RacExecutableRasClusterClient(runner, settings, NullLogger<RacExecutableRasClusterClient>.Instance);

        var sessions = await client.ListActiveSessionsAsync(default);

        sessions.Should().HaveCount(2);
        sessions.Single(s => s.AppId == "1CV8").ConsumesLicense.Should().BeTrue();
        sessions.Single(s => s.AppId == "BackgroundJob").ConsumesLicense.Should().BeFalse();
    }

    [Fact]
    public async Task ListActiveSessionsAsync_returns_empty_when_cluster_list_fails()
    {
        var settings = BuildSettings();
        var runner = Substitute.For<IRacProcessRunner>();
        runner.RunAsync(Arg.Any<string>(), Arg.Any<IReadOnlyList<string>>(), Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>())
            .Returns(new RacInvocation(
                ExitCode: 255,
                Stdout: string.Empty,
                Stderr: "Ошибка соединения с сервером"));

        var client = new RacExecutableRasClusterClient(runner, settings, NullLogger<RacExecutableRasClusterClient>.Instance);

        var sessions = await client.ListActiveSessionsAsync(default);

        sessions.Should().BeEmpty();
        // Session list НЕ должен дёргаться если cluster list уже сфейлил.
        await runner.Received(1)
            .RunAsync(Arg.Any<string>(), Arg.Any<IReadOnlyList<string>>(), Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ListActiveSessionsAsync_returns_empty_when_exe_path_not_configured()
    {
        // Дефолт OneCRasExePath сброшен в PR 3.8 — оператор может ещё не задать.
        var settings = Substitute.For<ISettingsSnapshot>();
        settings.GetString(SettingKey.OneCRasExePath).Returns((string?)null);

        var runner = Substitute.For<IRacProcessRunner>();
        var client = new RacExecutableRasClusterClient(runner, settings, NullLogger<RacExecutableRasClusterClient>.Instance);

        var sessions = await client.ListActiveSessionsAsync(default);

        sessions.Should().BeEmpty();
        await runner.DidNotReceiveWithAnyArgs()
            .RunAsync(default!, default!, default, default);
    }

    [Fact]
    public async Task KillSessionAsync_returns_killed_on_exit_zero()
    {
        var settings = BuildSettings();
        var runner = BuildRunner(clusterList: FakeClusterListStdout, sessionTerminateExit: 0, sessionTerminateStderr: string.Empty);
        var client = new RacExecutableRasClusterClient(runner, settings, NullLogger<RacExecutableRasClusterClient>.Instance);

        var descriptor = new SessionDescriptor(
            ClusterInfobaseId: Guid.NewGuid(),
            SessionId: Guid.Parse("492af167-20e6-497a-9eef-20ce4e930c6a"),
            AppId: "1CV8C",
            StartedAtUtc: DateTime.UtcNow);

        var result = await client.KillSessionAsync(descriptor, default);

        result.Killed.Should().BeTrue();
        result.AlreadyGone.Should().BeFalse();
    }

    [Fact]
    public async Task KillSessionAsync_returns_AlreadyGone_when_stderr_says_session_not_found()
    {
        // Captured stderr из реального rac.exe session terminate против несуществующего UUID.
        const string sessionNotFoundStderr = "Сеанс с указанным идентификатором не найден\r\n";

        var settings = BuildSettings();
        var runner = BuildRunner(
            clusterList: FakeClusterListStdout,
            sessionTerminateExit: 255,
            sessionTerminateStderr: sessionNotFoundStderr);

        var client = new RacExecutableRasClusterClient(runner, settings, NullLogger<RacExecutableRasClusterClient>.Instance);

        var result = await client.KillSessionAsync(
            new SessionDescriptor(Guid.NewGuid(), Guid.NewGuid(), "1CV8C", DateTime.UtcNow),
            default);

        result.Killed.Should().BeFalse();
        result.AlreadyGone.Should().BeTrue("идемпотентная семантика — сеанс уже завершён");
    }

    [Fact]
    public async Task KillSessionAsync_returns_not_killed_on_other_failure()
    {
        var settings = BuildSettings();
        var runner = BuildRunner(
            clusterList: FakeClusterListStdout,
            sessionTerminateExit: 255,
            sessionTerminateStderr: "Какая-то другая ошибка от rac.exe");

        var client = new RacExecutableRasClusterClient(runner, settings, NullLogger<RacExecutableRasClusterClient>.Instance);

        var result = await client.KillSessionAsync(
            new SessionDescriptor(Guid.NewGuid(), Guid.NewGuid(), "1CV8C", DateTime.UtcNow),
            default);

        result.Killed.Should().BeFalse();
        result.AlreadyGone.Should().BeFalse();
    }

    [Fact]
    public async Task PingAsync_returns_Ok_true_on_cluster_list_exit_zero()
    {
        var settings = BuildSettings();
        var runner = Substitute.For<IRacProcessRunner>();
        runner.RunAsync(Arg.Any<string>(), Arg.Any<IReadOnlyList<string>>(), Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>())
            .Returns(new RacInvocation(0, FakeClusterListStdout, string.Empty));

        var client = new RacExecutableRasClusterClient(runner, settings, NullLogger<RacExecutableRasClusterClient>.Instance);

        var result = await client.PingAsync(default);

        result.Ok.Should().BeTrue();
        result.Error.Should().BeNull();
    }

    [Fact]
    public async Task PingAsync_returns_Ok_false_with_stderr_on_failure()
    {
        var settings = BuildSettings();
        var runner = Substitute.For<IRacProcessRunner>();
        runner.RunAsync(Arg.Any<string>(), Arg.Any<IReadOnlyList<string>>(), Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>())
            .Returns(new RacInvocation(255, string.Empty, "Ошибка соединения с сервером\r\n"));

        var client = new RacExecutableRasClusterClient(runner, settings, NullLogger<RacExecutableRasClusterClient>.Instance);

        var result = await client.PingAsync(default);

        result.Ok.Should().BeFalse();
        result.Error.Should().Be("Ошибка соединения с сервером");
    }

    [Fact]
    public async Task Args_include_endpoint_as_positional_first_and_cluster_user_pwd_when_set()
    {
        var settings = BuildSettings(rasEndpoint: "ras-host:1545", user: "admin", password: "hunter2");
        var captured = new List<IReadOnlyList<string>>();
        var runner = Substitute.For<IRacProcessRunner>();
        runner.RunAsync(Arg.Any<string>(), Arg.Do<IReadOnlyList<string>>(a => captured.Add(a)), Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>())
            .Returns(new RacInvocation(0, FakeClusterListStdout, string.Empty), new RacInvocation(0, string.Empty, string.Empty));

        var client = new RacExecutableRasClusterClient(runner, settings, NullLogger<RacExecutableRasClusterClient>.Instance);

        await client.ListActiveSessionsAsync(default);

        captured.Should().HaveCount(2, "cluster list + session list");
        captured[0].Should().HaveElementAt(0, "ras-host:1545").And.Contain("cluster").And.Contain("list");
        captured[1].Should().HaveElementAt(0, "ras-host:1545").And.Contain("session").And.Contain("list");
        captured[1].Should().ContainMatch("--cluster=*").And.Contain("--cluster-user=admin").And.Contain("--cluster-pwd=hunter2");
    }

    [Fact]
    public async Task Args_omit_auth_flags_when_creds_blank()
    {
        // Локальный тест-rig с не-зарегистрированными админами: пустые user/password.
        var settings = BuildSettings(user: null, password: null);
        var captured = new List<IReadOnlyList<string>>();
        var runner = Substitute.For<IRacProcessRunner>();
        runner.RunAsync(Arg.Any<string>(), Arg.Do<IReadOnlyList<string>>(a => captured.Add(a)), Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>())
            .Returns(new RacInvocation(0, FakeClusterListStdout, string.Empty), new RacInvocation(0, string.Empty, string.Empty));

        var client = new RacExecutableRasClusterClient(runner, settings, NullLogger<RacExecutableRasClusterClient>.Instance);

        await client.ListActiveSessionsAsync(default);

        captured[1].Should().NotContainMatch("--cluster-user=*");
        captured[1].Should().NotContainMatch("--cluster-pwd=*");
    }

    [Fact]
    public async Task Cancellation_token_propagates_to_runner()
    {
        var settings = BuildSettings();
        var runner = Substitute.For<IRacProcessRunner>();
        var cts = new CancellationTokenSource();
        cts.Cancel();
        runner.RunAsync(Arg.Any<string>(), Arg.Any<IReadOnlyList<string>>(), Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>())
            .Returns<Task<RacInvocation>>(_ => Task.FromCanceled<RacInvocation>(cts.Token));

        var client = new RacExecutableRasClusterClient(runner, settings, NullLogger<RacExecutableRasClusterClient>.Instance);

        var act = async () => await client.ListActiveSessionsAsync(cts.Token);

        await act.Should().ThrowAsync<TaskCanceledException>();
    }

    // --- helpers ---

    private static ISettingsSnapshot BuildSettings(
        string? rasEndpoint = "localhost:1545",
        string? user = "admin",
        string? password = "secret")
    {
        var settings = Substitute.For<ISettingsSnapshot>();
        settings.GetString(SettingKey.OneCRasExePath).Returns("rac.exe");
        settings.GetString(SettingKey.OneCRasEndpoint).Returns(rasEndpoint);
        settings.GetString(SettingKey.OneCClusterAdminUser).Returns(user);
        settings.GetString(SettingKey.OneCClusterAdminPassword).Returns(password);
        return settings;
    }

    private static IRacProcessRunner BuildRunner(
        string clusterList,
        string? sessionList = null,
        int sessionTerminateExit = 0,
        string sessionTerminateStderr = "")
    {
        var runner = Substitute.For<IRacProcessRunner>();
        runner.RunAsync(Arg.Any<string>(),
                Arg.Is<IReadOnlyList<string>>(a => a.Contains("cluster") && a.Contains("list")),
                Arg.Any<TimeSpan>(),
                Arg.Any<CancellationToken>())
            .Returns(new RacInvocation(0, clusterList, string.Empty));

        if (sessionList is not null)
        {
            runner.RunAsync(Arg.Any<string>(),
                    Arg.Is<IReadOnlyList<string>>(a => a.Contains("session") && a.Contains("list")),
                    Arg.Any<TimeSpan>(),
                    Arg.Any<CancellationToken>())
                .Returns(new RacInvocation(0, sessionList, string.Empty));
        }

        runner.RunAsync(Arg.Any<string>(),
                Arg.Is<IReadOnlyList<string>>(a => a.Contains("session") && a.Contains("terminate")),
                Arg.Any<TimeSpan>(),
                Arg.Any<CancellationToken>())
            .Returns(new RacInvocation(sessionTerminateExit, string.Empty, sessionTerminateStderr));

        return runner;
    }
}
