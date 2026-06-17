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
        "host    : host-01\r\n" +
        "port    : 1541\r\n" +
        "name    : \"Локальный кластер\"\r\n";

    private const string FakeSingleSessionStdout =
        "session       : 492af167-20e6-497a-9eef-20ce4e930c6a\r\n" +
        "infobase      : 6256b6f3-dde1-41f9-a6c2-bdfc36bca7aa\r\n" +
        "app-id        : 1CV8C\r\n" +
        "user-name     : Иванов\r\n" +
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

        var client = BuildClient(runner, settings);

        var sessions = await client.ListActiveSessionsAsync(default);

        sessions.Should().HaveCount(1);
        var s = sessions[0];
        s.SessionId.Should().Be(Guid.Parse("492af167-20e6-497a-9eef-20ce4e930c6a"));
        s.ClusterInfobaseId.Should().Be(Guid.Parse("6256b6f3-dde1-41f9-a6c2-bdfc36bca7aa"));
        s.AppId.Should().Be("1CV8C");
        s.UserName.Should().Be("Иванов");
        s.Host.Should().Be("workstation01");
        s.StartedAtUtc.Kind.Should().Be(DateTimeKind.Utc);
        // started-at от rac.exe — локальное время сервера → конвертируется в UTC.
        // Детерминированно на любой машине (на UTC-CI local==UTC).
        var expectedUtc = DateTime.SpecifyKind(
            new DateTime(2026, 5, 21, 3, 39, 49), DateTimeKind.Local).ToUniversalTime();
        s.StartedAtUtc.Should().Be(expectedUtc);
    }

    // --- ADR-48 (MLC-166): факт лицензий через `session list --licenses` ---

    [Fact]
    public async Task ListLicensedSessionIdsAsync_returns_session_ids_with_license_block()
    {
        // Лицензионный сеанс присутствует с блоком license-type; нелицензионный в вывод
        // rac --licenses не попадает вовсе → отсутствует в множестве.
        const string licensesStdout =
            "session            : 11111111-1111-1111-1111-111111111111\r\n" +
            "user-name          : Иванов\r\n" +
            "app-id             : 1CV8C\r\n" +
            "license-type       : HASP\r\n" +
            "short-presentation : Клиентская лицензия\r\n";

        var settings = BuildSettings();
        var runner = Substitute.For<IRacProcessRunner>();
        runner.RunAsync("rac.exe", Arg.Is<IReadOnlyList<string>>(a => a.Contains("cluster") && a.Contains("list")), Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>())
            .Returns(new RacInvocation(0, FakeClusterListStdout, string.Empty));
        runner.RunAsync("rac.exe", Arg.Is<IReadOnlyList<string>>(a => a.Contains("session") && a.Contains("list") && a.Contains("--licenses")), Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>())
            .Returns(new RacInvocation(0, licensesStdout, string.Empty));

        var client = BuildClient(runner, settings);

        var licensed = await client.ListLicensedSessionIdsAsync(default);

        licensed.Should().NotBeNull();
        licensed!.Should().ContainSingle()
            .Which.Should().Be(Guid.Parse("11111111-1111-1111-1111-111111111111"));
    }

    [Fact]
    public async Task ListLicensedSessionIdsAsync_excludes_records_without_license_block()
    {
        // Защитно (ADR-48): запись без license-type И без short-presentation в множество
        // не попадает, даже если по какой-то причине оказалась в выводе.
        const string mixedStdout =
            "session            : 11111111-1111-1111-1111-111111111111\r\n" +
            "license-type       : HASP\r\n" +
            "\r\n" +
            "session            : 22222222-2222-2222-2222-222222222222\r\n" +
            "app-id             : BackgroundJob\r\n";

        var settings = BuildSettings();
        var runner = Substitute.For<IRacProcessRunner>();
        runner.RunAsync("rac.exe", Arg.Is<IReadOnlyList<string>>(a => a.Contains("cluster") && a.Contains("list")), Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>())
            .Returns(new RacInvocation(0, FakeClusterListStdout, string.Empty));
        runner.RunAsync("rac.exe", Arg.Is<IReadOnlyList<string>>(a => a.Contains("session") && a.Contains("list") && a.Contains("--licenses")), Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>())
            .Returns(new RacInvocation(0, mixedStdout, string.Empty));

        var client = BuildClient(runner, settings);

        var licensed = await client.ListLicensedSessionIdsAsync(default);

        licensed!.Should().ContainSingle()
            .Which.Should().Be(Guid.Parse("11111111-1111-1111-1111-111111111111"));
    }

    [Fact]
    public async Task ListLicensedSessionIdsAsync_returns_null_and_invalidates_cache_on_error()
    {
        // exit≠0 → null («факт недоступен», enforcement приостановится) + инвалидация UUID.
        var settings = BuildSettings();
        var runner = Substitute.For<IRacProcessRunner>();
        runner.RunAsync(Arg.Any<string>(), Arg.Is<IReadOnlyList<string>>(a => a.Contains("cluster") && a.Contains("list")), Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>())
            .Returns(new RacInvocation(0, FakeClusterListStdout, string.Empty));
        runner.RunAsync(Arg.Any<string>(), Arg.Is<IReadOnlyList<string>>(a => a.Contains("session") && a.Contains("list") && a.Contains("--licenses")), Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>())
            .Returns(new RacInvocation(255, string.Empty, "Ошибка соединения с сервером"));

        var cache = new ClusterUuidCache();
        var client = BuildClient(runner, settings, cache);

        var first = await client.ListLicensedSessionIdsAsync(default); // licenses fail → Invalidate
        await client.ListLicensedSessionIdsAsync(default);             // кэш сброшен → перерезолв

        first.Should().BeNull();
        await runner.Received(2).RunAsync(Arg.Any<string>(),
            Arg.Is<IReadOnlyList<string>>(a => a.Contains("cluster") && a.Contains("list")),
            Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ListLicensedSessionIdsAsync_returns_null_when_exe_path_missing()
    {
        var settings = Substitute.For<ISettingsSnapshot>();
        settings.GetString(SettingKey.OneCRasExePath).Returns((string?)null);
        var runner = Substitute.For<IRacProcessRunner>();
        var client = BuildClient(runner, settings);

        var licensed = await client.ListLicensedSessionIdsAsync(default);

        licensed.Should().BeNull();
        await runner.DidNotReceiveWithAnyArgs().RunAsync(default!, default!, default, default);
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

        var client = BuildClient(runner, settings);

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
        var client = BuildClient(runner, settings);

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
        var client = BuildClient(runner, settings);

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

        var client = BuildClient(runner, settings);

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

        var client = BuildClient(runner, settings);

        var result = await client.KillSessionAsync(
            new SessionDescriptor(Guid.NewGuid(), Guid.NewGuid(), "1CV8C", DateTime.UtcNow),
            default);

        result.Killed.Should().BeFalse();
        result.AlreadyGone.Should().BeFalse();
    }

    // --- MLC-190: текст-причина (--error-message) на enforcement-пути ---

    [Fact]
    public async Task KillSessionAsync_passes_error_message_arg_when_text_provided()
    {
        var settings = BuildSettings();
        var captured = new List<IReadOnlyList<string>>();
        var runner = Substitute.For<IRacProcessRunner>();
        runner.RunAsync(Arg.Any<string>(), Arg.Do<IReadOnlyList<string>>(a => captured.Add(a)), Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>())
            .Returns(new RacInvocation(0, FakeClusterListStdout, string.Empty), new RacInvocation(0, string.Empty, string.Empty));

        var client = BuildClient(runner, settings);

        await client.KillSessionAsync(
            new SessionDescriptor(Guid.NewGuid(), Guid.NewGuid(), "1CV8C", DateTime.UtcNow),
            default,
            errorMessage: "Лимит лицензий. Звоните 8-800.");

        // captured[0] = cluster list (резолв UUID), captured[1] = session terminate.
        captured.Should().HaveCount(2);
        captured[1].Should().Contain("session").And.Contain("terminate")
            .And.Contain("--error-message=Лимит лицензий. Звоните 8-800.");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task KillSessionAsync_omits_error_message_arg_when_text_blank(string? message)
    {
        var settings = BuildSettings();
        var captured = new List<IReadOnlyList<string>>();
        var runner = Substitute.For<IRacProcessRunner>();
        runner.RunAsync(Arg.Any<string>(), Arg.Do<IReadOnlyList<string>>(a => captured.Add(a)), Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>())
            .Returns(new RacInvocation(0, FakeClusterListStdout, string.Empty), new RacInvocation(0, string.Empty, string.Empty));

        var client = BuildClient(runner, settings);

        await client.KillSessionAsync(
            new SessionDescriptor(Guid.NewGuid(), Guid.NewGuid(), "1CV8C", DateTime.UtcNow),
            default,
            errorMessage: message);

        captured.Should().HaveCount(2);
        captured[1].Should().NotContainMatch("--error-message=*",
            "пустая настройка = не передавать флаг (текущее поведение)");
    }

    [Fact]
    public async Task KillSessionAsync_omits_error_message_arg_by_default()
    {
        // Дефолт параметра (ручное завершение оператором зовёт KillSessionAsync(descriptor, ct))
        // — текст не передаётся.
        var settings = BuildSettings();
        var captured = new List<IReadOnlyList<string>>();
        var runner = Substitute.For<IRacProcessRunner>();
        runner.RunAsync(Arg.Any<string>(), Arg.Do<IReadOnlyList<string>>(a => captured.Add(a)), Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>())
            .Returns(new RacInvocation(0, FakeClusterListStdout, string.Empty), new RacInvocation(0, string.Empty, string.Empty));

        var client = BuildClient(runner, settings);

        await client.KillSessionAsync(
            new SessionDescriptor(Guid.NewGuid(), Guid.NewGuid(), "1CV8C", DateTime.UtcNow),
            default);

        captured.Should().HaveCount(2);
        captured[1].Should().NotContainMatch("--error-message=*");
    }

    [Fact]
    public async Task PingAsync_returns_Ok_true_on_cluster_list_exit_zero()
    {
        var settings = BuildSettings();
        var runner = Substitute.For<IRacProcessRunner>();
        runner.RunAsync(Arg.Any<string>(), Arg.Any<IReadOnlyList<string>>(), Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>())
            .Returns(new RacInvocation(0, FakeClusterListStdout, string.Empty));

        var client = BuildClient(runner, settings);

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

        var client = BuildClient(runner, settings);

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

        var client = BuildClient(runner, settings);

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

        var client = BuildClient(runner, settings);

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

        var client = BuildClient(runner, settings);

        var act = async () => await client.ListActiveSessionsAsync(cts.Token);

        await act.Should().ThrowAsync<TaskCanceledException>();
    }

    // --- MLC-041: кэш резолва UUID кластера между вызовами ---

    [Fact]
    public async Task Cluster_uuid_is_cached_across_calls_no_repeat_cluster_list()
    {
        var settings = BuildSettings();
        var runner = BuildRunner(clusterList: FakeClusterListStdout, sessionList: FakeSingleSessionStdout);
        var cache = new ClusterUuidCache();
        var client = BuildClient(runner, settings, cache);

        await client.ListActiveSessionsAsync(default);
        await client.ListActiveSessionsAsync(default);

        // cluster list резолвится один раз — второй вызов берёт UUID из кэша.
        await runner.Received(1).RunAsync(Arg.Any<string>(),
            Arg.Is<IReadOnlyList<string>>(a => a.Contains("cluster") && a.Contains("list")),
            Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>());
        await runner.Received(2).RunAsync(Arg.Any<string>(),
            Arg.Is<IReadOnlyList<string>>(a => a.Contains("session") && a.Contains("list")),
            Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Cluster_uuid_cache_invalidates_on_endpoint_change()
    {
        var endpoint = "ras-a:1545";
        var settings = Substitute.For<ISettingsSnapshot>();
        settings.GetString(SettingKey.OneCRasExePath).Returns("rac.exe");
        settings.GetString(SettingKey.OneCClusterAdminUser).Returns("admin");
        settings.GetString(SettingKey.OneCClusterAdminPassword).Returns("secret");
        settings.GetString(SettingKey.OneCRasEndpoint).Returns(_ => endpoint);

        var runner = BuildRunner(clusterList: FakeClusterListStdout, sessionList: FakeSingleSessionStdout);
        var cache = new ClusterUuidCache();
        var client = BuildClient(runner, settings, cache);

        await client.ListActiveSessionsAsync(default);
        endpoint = "ras-b:1545"; // оператор сменил endpoint → ключ кэша больше не совпадает
        await client.ListActiveSessionsAsync(default);

        // Смена endpoint → промах кэша → повторный резолв cluster list.
        await runner.Received(2).RunAsync(Arg.Any<string>(),
            Arg.Is<IReadOnlyList<string>>(a => a.Contains("cluster") && a.Contains("list")),
            Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Cluster_uuid_cache_invalidates_after_command_error()
    {
        var settings = BuildSettings();
        var runner = Substitute.For<IRacProcessRunner>();
        runner.RunAsync(Arg.Any<string>(),
                Arg.Is<IReadOnlyList<string>>(a => a.Contains("cluster") && a.Contains("list")),
                Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>())
            .Returns(new RacInvocation(0, FakeClusterListStdout, string.Empty));
        runner.RunAsync(Arg.Any<string>(),
                Arg.Is<IReadOnlyList<string>>(a => a.Contains("session") && a.Contains("list")),
                Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>())
            .Returns(new RacInvocation(255, string.Empty, "Ошибка соединения с сервером"));

        var cache = new ClusterUuidCache();
        var client = BuildClient(runner, settings, cache);

        await client.ListActiveSessionsAsync(default); // резолв OK → session list fail → Invalidate
        await client.ListActiveSessionsAsync(default); // кэш сброшен → перерезолв

        await runner.Received(2).RunAsync(Arg.Any<string>(),
            Arg.Is<IReadOnlyList<string>>(a => a.Contains("cluster") && a.Contains("list")),
            Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Kill_AlreadyGone_does_not_invalidate_cluster_uuid_cache()
    {
        // Идемпотентный no-op «Сеанс … не найден» — НЕ ошибка кластера: кэш переживает,
        // следующий kill не перерезолвит cluster list.
        var settings = BuildSettings();
        var runner = BuildRunner(
            clusterList: FakeClusterListStdout,
            sessionTerminateExit: 255,
            sessionTerminateStderr: "Сеанс с указанным идентификатором не найден\r\n");
        var cache = new ClusterUuidCache();
        var client = BuildClient(runner, settings, cache);
        var descriptor = new SessionDescriptor(Guid.NewGuid(), Guid.NewGuid(), "1CV8C", DateTime.UtcNow);

        var first = await client.KillSessionAsync(descriptor, default);
        var second = await client.KillSessionAsync(descriptor, default);

        first.AlreadyGone.Should().BeTrue();
        second.AlreadyGone.Should().BeTrue();
        await runner.Received(1).RunAsync(Arg.Any<string>(),
            Arg.Is<IReadOnlyList<string>>(a => a.Contains("cluster") && a.Contains("list")),
            Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>());
        await runner.Received(2).RunAsync(Arg.Any<string>(),
            Arg.Is<IReadOnlyList<string>>(a => a.Contains("session") && a.Contains("terminate")),
            Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>());
    }

    // --- MLC-066: раздел «Быстродействие» — session loads + process list ---

    private const string FakeProcessListStdout =
        "process              : 487281d5-aaaa-bbbb-cccc-ddddeeeeffff\r\n" +
        "pid                  : 15876\r\n" +
        "available-perfomance : 416\r\n" +
        "memory-size          : 1682404\r\n" +
        "avg-call-time        : 1.124\r\n";

    [Fact]
    public async Task ListSessionLoadsAsync_resolves_cluster_then_lists_sessions_with_perf()
    {
        const string loaded =
            "session          : 02d5184c-65b5-4d8a-ae39-b156b909fcaf\r\n" +
            "session-id       : 1\r\n" +
            "infobase         : 6256b6f3-dde1-41f9-a6c2-bdfc36bca7aa\r\n" +
            "app-id           : 1CV8C\r\n" +
            "cpu-time-current : 109\r\n" +
            "memory-current   : -1138560\r\n";

        var settings = BuildSettings();
        var runner = BuildRunner(clusterList: FakeClusterListStdout, sessionList: loaded);
        var client = BuildClient(runner, settings);

        var sessions = await client.ListSessionLoadsAsync(default);

        var s = sessions.Should().ContainSingle().Subject;
        s.SessionId.Should().Be(Guid.Parse("02d5184c-65b5-4d8a-ae39-b156b909fcaf"));
        s.CpuTimeCurrent.Should().Be(109);
        s.MemoryCurrent.Should().Be(-1138560);
    }

    [Fact]
    public async Task ListProcessesAsync_resolves_cluster_then_runs_process_list_and_maps()
    {
        var settings = BuildSettings();
        var runner = Substitute.For<IRacProcessRunner>();
        runner.RunAsync("rac.exe", Arg.Is<IReadOnlyList<string>>(a => a.Contains("cluster") && a.Contains("list")), Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>())
            .Returns(new RacInvocation(0, FakeClusterListStdout, string.Empty));
        runner.RunAsync("rac.exe", Arg.Is<IReadOnlyList<string>>(a => a.Contains("process") && a.Contains("list")), Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>())
            .Returns(new RacInvocation(0, FakeProcessListStdout, string.Empty));

        var client = BuildClient(runner, settings);

        var processes = await client.ListProcessesAsync(default);

        var p = processes.Should().ContainSingle().Subject;
        p.Pid.Should().Be(15876);
        p.AvailablePerformance.Should().Be(416);
        p.AvgCallTime.Should().Be(1.124);
    }

    [Fact]
    public async Task ListProcessesAsync_reuses_cached_cluster_uuid_no_extra_cluster_list()
    {
        // +1 спавн на poll = только process list при тёплом кэше (после session list).
        var settings = BuildSettings();
        var runner = Substitute.For<IRacProcessRunner>();
        runner.RunAsync(Arg.Any<string>(), Arg.Is<IReadOnlyList<string>>(a => a.Contains("cluster") && a.Contains("list")), Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>())
            .Returns(new RacInvocation(0, FakeClusterListStdout, string.Empty));
        runner.RunAsync(Arg.Any<string>(), Arg.Is<IReadOnlyList<string>>(a => a.Contains("session") && a.Contains("list")), Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>())
            .Returns(new RacInvocation(0, FakeSingleSessionStdout, string.Empty));
        runner.RunAsync(Arg.Any<string>(), Arg.Is<IReadOnlyList<string>>(a => a.Contains("process") && a.Contains("list")), Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>())
            .Returns(new RacInvocation(0, FakeProcessListStdout, string.Empty));

        var cache = new ClusterUuidCache();
        var client = BuildClient(runner, settings, cache);

        await client.ListSessionLoadsAsync(default); // тёплый резолв UUID
        await client.ListProcessesAsync(default);    // берёт UUID из кэша

        await runner.Received(1).RunAsync(Arg.Any<string>(),
            Arg.Is<IReadOnlyList<string>>(a => a.Contains("cluster") && a.Contains("list")),
            Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>());
        await runner.Received(1).RunAsync(Arg.Any<string>(),
            Arg.Is<IReadOnlyList<string>>(a => a.Contains("process") && a.Contains("list")),
            Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ListSessionLoadsAsync_returns_empty_when_exe_path_missing()
    {
        var settings = Substitute.For<ISettingsSnapshot>();
        settings.GetString(SettingKey.OneCRasExePath).Returns((string?)null);
        var runner = Substitute.For<IRacProcessRunner>();
        var client = BuildClient(runner, settings);

        var sessions = await client.ListSessionLoadsAsync(default);

        sessions.Should().BeEmpty();
        await runner.DidNotReceiveWithAnyArgs().RunAsync(default!, default!, default, default);
    }

    [Fact]
    public async Task ListProcessesAsync_returns_empty_and_invalidates_cache_on_error()
    {
        var settings = BuildSettings();
        var runner = Substitute.For<IRacProcessRunner>();
        runner.RunAsync(Arg.Any<string>(), Arg.Is<IReadOnlyList<string>>(a => a.Contains("cluster") && a.Contains("list")), Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>())
            .Returns(new RacInvocation(0, FakeClusterListStdout, string.Empty));
        runner.RunAsync(Arg.Any<string>(), Arg.Is<IReadOnlyList<string>>(a => a.Contains("process") && a.Contains("list")), Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>())
            .Returns(new RacInvocation(255, string.Empty, "Ошибка соединения с сервером"));

        var cache = new ClusterUuidCache();
        var client = BuildClient(runner, settings, cache);

        await client.ListProcessesAsync(default); // process list fail → Invalidate
        await client.ListProcessesAsync(default); // кэш сброшен → перерезолв cluster list

        await runner.Received(2).RunAsync(Arg.Any<string>(),
            Arg.Is<IReadOnlyList<string>>(a => a.Contains("cluster") && a.Contains("list")),
            Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>());
    }

    // --- helpers ---

    // Каждый тест получает свежий ClusterUuidCache (если не передан общий) — поведение
    // 1:1 с до-кэшевой версией; общий кэш передаётся явно в тестах кэша/инвалидации.
    private static RacExecutableRasClusterClient BuildClient(
        IRacProcessRunner runner, ISettingsSnapshot settings, IClusterUuidCache? cache = null)
        => new(runner, settings, cache ?? new ClusterUuidCache(),
            NullLogger<RacExecutableRasClusterClient>.Instance);

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

    // --- Discovery: ListInfobasesAsync / ParseInfobases (UID-picker) ---

    [Fact]
    public void ParseInfobases_maps_id_name_descr_and_skips_malformed()
    {
        const string stdout =
            "infobase : 44444444-4444-4444-4444-444444444444\r\nname : Бухгалтерия\r\ndescr : Прод база\r\n" +
            "\r\n" +
            "infobase : 55555555-5555-5555-5555-555555555555\r\nname : Зарплата\r\ndescr : \r\n" +
            "\r\n" +
            "name : Без UUID\r\ndescr : пропустить\r\n";

        var result = RacExecutableRasClusterClient.ParseInfobases(stdout);

        result.Should().HaveCount(2);
        result[0].Id.Should().Be(Guid.Parse("44444444-4444-4444-4444-444444444444"));
        result[0].Name.Should().Be("Бухгалтерия");
        result[0].Description.Should().Be("Прод база");
        result[1].Description.Should().BeNull("пустой descr → null");
    }

    [Fact]
    public void ParseInfobases_falls_back_to_uuid_when_name_missing()
    {
        const string stdout = "infobase : 66666666-6666-6666-6666-666666666666\r\n";

        var result = RacExecutableRasClusterClient.ParseInfobases(stdout);

        result.Should().ContainSingle();
        result[0].Name.Should().Be("66666666-6666-6666-6666-666666666666");
    }

    [Fact]
    public async Task ListInfobasesAsync_resolves_cluster_then_returns_available_list()
    {
        var settings = BuildSettings();
        var runner = Substitute.For<IRacProcessRunner>();
        runner.RunAsync("rac.exe", Arg.Is<IReadOnlyList<string>>(a => a.Contains("cluster") && a.Contains("list")), Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>())
            .Returns(new RacInvocation(0, FakeClusterListStdout, string.Empty));
        runner.RunAsync("rac.exe", Arg.Is<IReadOnlyList<string>>(a => a.Contains("infobase") && a.Contains("summary")), Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>())
            .Returns(new RacInvocation(0,
                "infobase : 77777777-7777-7777-7777-777777777777\r\nname : База А\r\n\r\ninfobase : 88888888-8888-8888-8888-888888888888\r\nname : База Б\r\n",
                string.Empty));

        var client = BuildClient(runner, settings);

        var result = await client.ListInfobasesAsync(default);

        result.Available.Should().BeTrue();
        result.Error.Should().BeNull();
        result.Infobases.Should().HaveCount(2);
        result.Infobases[0].Name.Should().Be("База А");
    }

    [Fact]
    public async Task ListInfobasesAsync_unavailable_when_exe_path_missing()
    {
        var settings = Substitute.For<ISettingsSnapshot>();
        settings.GetString(SettingKey.OneCRasExePath).Returns((string?)null);
        var runner = Substitute.For<IRacProcessRunner>();
        var client = BuildClient(runner, settings);

        var result = await client.ListInfobasesAsync(default);

        result.Available.Should().BeFalse();
        result.Infobases.Should().BeEmpty();
        result.Error.Should().NotBeNull();
    }

    [Fact]
    public async Task ListInfobasesAsync_unavailable_when_summary_list_fails()
    {
        var settings = BuildSettings();
        var runner = Substitute.For<IRacProcessRunner>();
        runner.RunAsync(Arg.Any<string>(), Arg.Is<IReadOnlyList<string>>(a => a.Contains("cluster") && a.Contains("list")), Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>())
            .Returns(new RacInvocation(0, FakeClusterListStdout, string.Empty));
        runner.RunAsync(Arg.Any<string>(), Arg.Is<IReadOnlyList<string>>(a => a.Contains("infobase") && a.Contains("summary")), Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>())
            .Returns(new RacInvocation(1, string.Empty, "ошибка авторизации администратора кластера"));

        var client = BuildClient(runner, settings);

        var result = await client.ListInfobasesAsync(default);

        result.Available.Should().BeFalse();
        result.Infobases.Should().BeEmpty();
        result.Error.Should().Contain("авторизации");
    }
}
