using FluentAssertions;
using MitLicenseCenter.Infrastructure.Clusters;
using Xunit;

namespace MitLicenseCenter.Tests.Unit.Clusters;

// MLC-066: маппинг perf-полей `rac session list` → OneCSessionLoad и `rac process list` →
// OneCProcessLoad. Фикстуры — из сырых срезов нагруженного стенда 8.5.1.1302 (секция
// «Результаты разведки (MLC-063)» план-файла). Проверяем гочи разведки: отрицательная
// `memory-current`, дробные/научная нотация `avg-call-time`, нулевой UUID привязки,
// отсутствующие поля (парсер «never throws», ADR-3.3).
public sealed class OneCLoadParsingTests
{
    // --- session list → OneCSessionLoad ---

    // Активный сеанс 1CV8C под нагрузкой: привязан к рабочему процессу, current-поля ожили,
    // memory-current отрицательная (момент GC — разведка видела −1138560).
    private const string LoadedActiveSession =
        "session                          : 02d5184c-65b5-4d8a-ae39-b156b909fcaf\r\n" +
        "session-id                       : 1\r\n" +
        "infobase                         : 6256b6f3-dde1-41f9-a6c2-bdfc36bca7aa\r\n" +
        "connection                       : b46dead9-1111-2222-3333-444455556666\r\n" +
        "process                          : 487281d5-aaaa-bbbb-cccc-ddddeeeeffff\r\n" +
        "user-name                        : Иванов\r\n" +
        "host                             : HOST-01\r\n" +
        "app-id                           : 1CV8C\r\n" +
        "duration-current                 : 422\r\n" +
        "duration-current-dbms            : 0\r\n" +
        "cpu-time-current                 : 109\r\n" +
        "memory-current                   : -1138560\r\n" +
        "read-current                     : 12532\r\n" +
        "blocked-by-dbms                  : 0\r\n" +
        "blocked-by-ls                    : 0\r\n" +
        "calls-all                        : 771\r\n" +
        "cpu-time-total                   : 17343\r\n" +
        "last-active-at                   : 2026-06-08T20:21:45\r\n";

    [Fact]
    public void ParseSessionLoads_maps_full_perf_fields_of_loaded_session()
    {
        var sessions = RacExecutableRasClusterClient.ParseSessionLoads(LoadedActiveSession);

        sessions.Should().ContainSingle();
        var s = sessions[0];
        s.SessionId.Should().Be(Guid.Parse("02d5184c-65b5-4d8a-ae39-b156b909fcaf"));
        s.SessionNumber.Should().Be(1);
        s.ClusterInfobaseId.Should().Be(Guid.Parse("6256b6f3-dde1-41f9-a6c2-bdfc36bca7aa"));
        s.AppId.Should().Be("1CV8C");
        s.UserName.Should().Be("Иванов");
        s.Host.Should().Be("HOST-01");
        s.Process.Should().Be(Guid.Parse("487281d5-aaaa-bbbb-cccc-ddddeeeeffff"));
        s.Connection.Should().Be(Guid.Parse("b46dead9-1111-2222-3333-444455556666"));
        s.CpuTimeCurrent.Should().Be(109);
        s.DurationCurrent.Should().Be(422);
        s.DurationCurrentDbms.Should().Be(0);
        s.BlockedByDbms.Should().Be(0);
        s.BlockedByLs.Should().Be(0);
        s.LastActiveAtUtc.Should().NotBeNull();
        s.LastActiveAtUtc!.Value.Kind.Should().Be(DateTimeKind.Utc);
    }

    [Fact]
    public void ParseSessionLoads_keeps_negative_memory_current()
    {
        // Гоча MLC-063: memory-current бывает отрицательной (GC) → знаковый long, не uint.
        var sessions = RacExecutableRasClusterClient.ParseSessionLoads(LoadedActiveSession);

        sessions[0].MemoryCurrent.Should().Be(-1138560);
    }

    [Fact]
    public void ParseSessionLoads_nulls_zero_process_and_connection_uuids()
    {
        // Удалённый клиент idle в момент снимка: привязка к рабочему процессу обнулена
        // (process/connection = 0000…), но статистика накоплена. Нулевой UUID → null.
        const string idleRemote =
            "session                          : 33333333-3333-3333-3333-333333333333\r\n" +
            "session-id                       : 4\r\n" +
            "infobase                         : 6256b6f3-dde1-41f9-a6c2-bdfc36bca7aa\r\n" +
            "connection                       : 00000000-0000-0000-0000-000000000000\r\n" +
            "process                          : 00000000-0000-0000-0000-000000000000\r\n" +
            "user-name                        : Влада\r\n" +
            "host                             : vlada-pc\r\n" +
            "app-id                           : 1CV8C\r\n" +
            "cpu-time-current                 : 0\r\n" +
            "last-active-at                   : 2026-06-08T20:15:00\r\n";

        var sessions = RacExecutableRasClusterClient.ParseSessionLoads(idleRemote);

        var s = sessions.Should().ContainSingle().Subject;
        s.Process.Should().BeNull();
        s.Connection.Should().BeNull();
        s.CpuTimeCurrent.Should().Be(0);
    }

    [Fact]
    public void ParseSessionLoads_leaves_perf_fields_null_when_absent()
    {
        // Иная версия/конфигурация платформы: только опорные поля, без perf — все nullable,
        // парсер не падает (ADR-3.3 «never throws»). Прецедент — пустая фикстура PR 3.8.
        const string bare =
            "session       : 44444444-4444-4444-4444-444444444444\r\n" +
            "infobase      : 6256b6f3-dde1-41f9-a6c2-bdfc36bca7aa\r\n" +
            "app-id        : 1CV8C\r\n" +
            "user-name     : Иванов\r\n";

        var sessions = RacExecutableRasClusterClient.ParseSessionLoads(bare);

        var s = sessions.Should().ContainSingle().Subject;
        s.SessionNumber.Should().BeNull();
        s.Process.Should().BeNull();
        s.CpuTimeCurrent.Should().BeNull();
        s.DurationCurrent.Should().BeNull();
        s.MemoryCurrent.Should().BeNull();
        s.BlockedByDbms.Should().BeNull();
        s.LastActiveAtUtc.Should().BeNull();
    }

    [Fact]
    public void ParseSessionLoads_skips_record_without_required_keys()
    {
        // Запись без session/infobase пропускается (как kill-маппинг).
        const string stdout =
            "user-name : Без сессии\r\napp-id : 1CV8C\r\n" +
            "\r\n" +
            "session : 55555555-5555-5555-5555-555555555555\r\ninfobase : 6256b6f3-dde1-41f9-a6c2-bdfc36bca7aa\r\n";

        var sessions = RacExecutableRasClusterClient.ParseSessionLoads(stdout);

        sessions.Should().ContainSingle();
        sessions[0].SessionId.Should().Be(Guid.Parse("55555555-5555-5555-5555-555555555555"));
    }

    [Fact]
    public void ParseSessionLoads_maps_two_sessions_with_blocking()
    {
        // Фоновое задание заблокировано другим сеансом по управляемой блокировке (blocked-by-ls≠0).
        const string twoSessions =
            "session          : 11111111-1111-1111-1111-111111111111\r\n" +
            "session-id       : 2\r\n" +
            "infobase         : 6256b6f3-dde1-41f9-a6c2-bdfc36bca7aa\r\n" +
            "app-id           : BackgroundJob\r\n" +
            "duration-current : 1468\r\n" +
            "blocked-by-dbms  : 0\r\n" +
            "blocked-by-ls    : 7\r\n" +
            "\r\n" +
            "session          : 22222222-2222-2222-2222-222222222222\r\n" +
            "session-id       : 7\r\n" +
            "infobase         : 6256b6f3-dde1-41f9-a6c2-bdfc36bca7aa\r\n" +
            "app-id           : 1CV8C\r\n" +
            "duration-current : 328\r\n";

        var sessions = RacExecutableRasClusterClient.ParseSessionLoads(twoSessions);

        sessions.Should().HaveCount(2);
        var bg = sessions.Single(x => x.AppId == "BackgroundJob");
        bg.DurationCurrent.Should().Be(1468);
        bg.BlockedByLs.Should().Be(7, "заблокирован сеансом 7 по управляемой блокировке");
        bg.BlockedByDbms.Should().Be(0);
    }

    // --- process list → OneCProcessLoad ---

    [Fact]
    public void ParseProcessLoads_maps_worker_process_fields()
    {
        // Сырой срез rphost под нагрузкой (разведка MLC-063).
        const string stdout =
            "process              : 487281d5-aaaa-bbbb-cccc-ddddeeeeffff\r\n" +
            "pid                  : 15876\r\n" +
            "available-perfomance : 416\r\n" +
            "capacity             : 1000\r\n" +
            "connections          : 11\r\n" +
            "memory-size          : 1682404\r\n" +
            "avg-call-time        : 1.124\r\n" +
            "avg-db-call-time     : 0.002\r\n";

        var processes = RacExecutableRasClusterClient.ParseProcessLoads(stdout);

        var p = processes.Should().ContainSingle().Subject;
        p.Process.Should().Be(Guid.Parse("487281d5-aaaa-bbbb-cccc-ddddeeeeffff"));
        p.Pid.Should().Be(15876);
        p.AvailablePerformance.Should().Be(416, "ключ rac с опечаткой `available-perfomance`");
        p.MemorySize.Should().Be(1682404);
        p.AvgCallTime.Should().Be(1.124);
    }

    [Fact]
    public void ParseProcessLoads_parses_fractional_and_scientific_avg_call_time_invariantly()
    {
        // Гоча MLC-063: avg-* дробные, встречается научная нотация — инвариантный парс
        // (точка-десятичный разделитель и экспонента, независимо от локали).
        const string stdout =
            "process       : 11111111-1111-1111-1111-111111111111\r\n" +
            "pid           : 100\r\n" +
            "avg-call-time : 9.99E-05\r\n" +
            "\r\n" +
            "process       : 22222222-2222-2222-2222-222222222222\r\n" +
            "pid           : 200\r\n" +
            "avg-call-time : 3.540\r\n";

        var processes = RacExecutableRasClusterClient.ParseProcessLoads(stdout);

        processes.Should().HaveCount(2);
        processes[0].AvgCallTime.Should().BeApproximately(9.99e-05, 1e-12);
        processes[1].AvgCallTime.Should().Be(3.540);
    }

    [Fact]
    public void ParseProcessLoads_leaves_fields_null_when_absent_and_skips_without_process()
    {
        const string stdout =
            "process : 33333333-3333-3333-3333-333333333333\r\n" +
            "\r\n" +
            "pid : 999\r\nmemory-size : 123\r\n"; // без process → пропуск

        var processes = RacExecutableRasClusterClient.ParseProcessLoads(stdout);

        var p = processes.Should().ContainSingle().Subject;
        p.Process.Should().Be(Guid.Parse("33333333-3333-3333-3333-333333333333"));
        p.Pid.Should().BeNull();
        p.AvailablePerformance.Should().BeNull();
        p.AvgCallTime.Should().BeNull();
        p.MemorySize.Should().BeNull();
    }

    [Fact]
    public void Parse_methods_return_empty_on_empty_stdout()
    {
        RacExecutableRasClusterClient.ParseSessionLoads(string.Empty).Should().BeEmpty();
        RacExecutableRasClusterClient.ParseProcessLoads(string.Empty).Should().BeEmpty();
    }
}
