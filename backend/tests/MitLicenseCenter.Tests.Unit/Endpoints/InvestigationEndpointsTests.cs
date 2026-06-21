using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using MitLicenseCenter.Application.TechLog;
using MitLicenseCenter.Domain.Audit;
using MitLicenseCenter.Domain.Infobases;
using MitLicenseCenter.Domain.TechLog;
using MitLicenseCenter.Infrastructure.Persistence;
using MitLicenseCenter.Web.Endpoints;
using NSubstitute;
using Xunit;

namespace MitLicenseCenter.Tests.Unit.Endpoints;

// MLC-239 (трек 1.2, этап C): эндпоинты «Расследования». Стиль проекта — invoke internal static handler
// напрямую (как PerfRecordingEndpointsTests). Старт/стоп идут через ITechLogCollectionService (substitute —
// проводка исхода); список/деталь/отчёт/прогресс/удаление читают AppDbContext напрямую (vertical slice).
// Поведение самого сервиса/конвейера — InvestigationOrchestrationTests / TechLogCollectionServiceTests.
public sealed class InvestigationEndpointsTests
{
    private static readonly DateTime T0 = new(2026, 6, 20, 10, 0, 0, DateTimeKind.Utc);

    // ── Старт ───────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Start_resolves_infobase_and_carries_tenant_and_infobase_ids()
    {
        using var db = TestHelpers.NewInMemoryDb();
        var tenantId = Guid.NewGuid();
        var infobaseId = Guid.NewGuid();
        // Резолв старта читает только db.Infobases (TenantId/Name берутся из строки ИБ); Tenant-строка не нужна.
        db.Infobases.Add(new Infobase
        {
            Id = infobaseId,
            TenantId = tenantId,
            Name = "infobase01",
            DatabaseName = "infobase01_db",
            Status = InfobaseStatus.Active,
        });
        await db.SaveChangesAsync();

        var caseId = Guid.NewGuid();
        var svc = Substitute.For<ITechLogCollectionService>();
        svc.InstallAsync(
                Arg.Any<string>(), Arg.Any<TechLogScenario>(), Arg.Any<string?>(), Arg.Any<CancellationToken>(),
                Arg.Any<Guid?>(), Arg.Any<Guid?>(), Arg.Any<long?>())
            .Returns(new TechLogStartResult(TechLogStartOutcome.Started, caseId));

        // Сервис персистит дело в своём scope; in-memory store общий — эмулируем строкой с резолвнутыми привязками.
        db.Investigations.Add(new Investigation
        {
            Id = caseId,
            Scenario = InvestigationScenario.SlowQueries,
            Status = InvestigationStatus.Collecting,
            StartedAtUtc = T0,
            StartedBy = "admin",
            TenantId = tenantId,
            InfobaseId = infobaseId,
            InfobaseProcessName = "infobase01",
            CollectionDirectory = @"C:\techlog",
            ConfigMarker = "marker",
        });
        await db.SaveChangesAsync();

        var result = await InvestigationEndpoints.StartInvestigationAsync(
            new StartInvestigationRequest(InvestigationScenario.SlowQueries, infobaseId),
            svc, db, TestHelpers.NewHttpContext("admin"), CancellationToken.None);

        var created = result.Result.Should().BeOfType<Created<InvestigationSummary>>().Subject;
        created.Value!.Id.Should().Be(caseId);
        created.Value!.TenantId.Should().Be(tenantId);
        created.Value!.InfobaseId.Should().Be(infobaseId);
        created.Location.Should().Be($"/api/v1/investigations/{caseId}");

        // Резолв передал имя ИБ как p:processName и InfobaseId/TenantId в сервис (закрытие разрыва MLC-238).
        // Порог не задан → null (сервис применит дефолт 1 c, MLC-248).
        await svc.Received(1).InstallAsync(
            "admin", TechLogScenario.SlowQueries, "infobase01", Arg.Any<CancellationToken>(), infobaseId, tenantId, null);
    }

    [Fact]
    public async Task Start_passes_threshold_seconds_as_microseconds_to_service()
    {
        using var db = TestHelpers.NewInMemoryDb();
        var caseId = Guid.NewGuid();
        var svc = Substitute.For<ITechLogCollectionService>();
        svc.InstallAsync(
                Arg.Any<string>(), Arg.Any<TechLogScenario>(), Arg.Any<string?>(), Arg.Any<CancellationToken>(),
                Arg.Any<Guid?>(), Arg.Any<Guid?>(), Arg.Any<long?>())
            .Returns(new TechLogStartResult(TechLogStartOutcome.Started, caseId));
        db.Investigations.Add(NewCase(caseId, InvestigationStatus.Collecting));
        await db.SaveChangesAsync();

        // 2.5 c → 2 500 000 µs (конверсия сек→микросек на эндпоинте).
        var result = await InvestigationEndpoints.StartInvestigationAsync(
            new StartInvestigationRequest(InvestigationScenario.SlowQueries, InfobaseId: null, SlowQueryThresholdSeconds: 2.5),
            svc, db, TestHelpers.NewHttpContext("admin"), CancellationToken.None);

        result.Result.Should().BeOfType<Created<InvestigationSummary>>();
        await svc.Received(1).InstallAsync(
            "admin", TechLogScenario.SlowQueries, null, Arg.Any<CancellationToken>(), null, null, 2_500_000L);
    }

    [Fact]
    public async Task Start_without_threshold_passes_null_so_service_applies_default()
    {
        using var db = TestHelpers.NewInMemoryDb();
        var caseId = Guid.NewGuid();
        var svc = Substitute.For<ITechLogCollectionService>();
        svc.InstallAsync(
                Arg.Any<string>(), Arg.Any<TechLogScenario>(), Arg.Any<string?>(), Arg.Any<CancellationToken>(),
                Arg.Any<Guid?>(), Arg.Any<Guid?>(), Arg.Any<long?>())
            .Returns(new TechLogStartResult(TechLogStartOutcome.Started, caseId));
        db.Investigations.Add(NewCase(caseId, InvestigationStatus.Collecting));
        await db.SaveChangesAsync();

        var result = await InvestigationEndpoints.StartInvestigationAsync(
            new StartInvestigationRequest(InvestigationScenario.SlowQueries, InfobaseId: null),
            svc, db, TestHelpers.NewHttpContext("admin"), CancellationToken.None);

        result.Result.Should().BeOfType<Created<InvestigationSummary>>();
        await svc.Received(1).InstallAsync(
            "admin", TechLogScenario.SlowQueries, null, Arg.Any<CancellationToken>(), null, null, null);
    }

    [Fact]
    public async Task Start_with_zero_threshold_is_valid_and_passes_zero_micros()
    {
        using var db = TestHelpers.NewInMemoryDb();
        var caseId = Guid.NewGuid();
        var svc = Substitute.For<ITechLogCollectionService>();
        svc.InstallAsync(
                Arg.Any<string>(), Arg.Any<TechLogScenario>(), Arg.Any<string?>(), Arg.Any<CancellationToken>(),
                Arg.Any<Guid?>(), Arg.Any<Guid?>(), Arg.Any<long?>())
            .Returns(new TechLogStartResult(TechLogStartOutcome.Started, caseId));
        db.Investigations.Add(NewCase(caseId, InvestigationStatus.Collecting));
        await db.SaveChangesAsync();

        // Явный 0 допустим (все запросы в топ); конверсия = 0 µs.
        var result = await InvestigationEndpoints.StartInvestigationAsync(
            new StartInvestigationRequest(InvestigationScenario.GeneralSlow, InfobaseId: null, SlowQueryThresholdSeconds: 0),
            svc, db, TestHelpers.NewHttpContext("admin"), CancellationToken.None);

        result.Result.Should().BeOfType<Created<InvestigationSummary>>();
        await svc.Received(1).InstallAsync(
            "admin", TechLogScenario.GeneralSlow, null, Arg.Any<CancellationToken>(), null, null, 0L);
    }

    [Fact]
    public async Task Start_with_negative_threshold_returns_validation_problem_and_does_not_install()
    {
        using var db = TestHelpers.NewInMemoryDb();
        var svc = Substitute.For<ITechLogCollectionService>();

        var result = await InvestigationEndpoints.StartInvestigationAsync(
            new StartInvestigationRequest(InvestigationScenario.SlowQueries, InfobaseId: null, SlowQueryThresholdSeconds: -1),
            svc, db, TestHelpers.NewHttpContext("admin"), CancellationToken.None);

        result.Result.Should().BeOfType<ValidationProblem>();
        await svc.DidNotReceive().InstallAsync(
            Arg.Any<string>(), Arg.Any<TechLogScenario>(), Arg.Any<string?>(), Arg.Any<CancellationToken>(),
            Arg.Any<Guid?>(), Arg.Any<Guid?>(), Arg.Any<long?>());
    }

    [Fact]
    public async Task Start_without_infobase_collects_whole_cluster()
    {
        using var db = TestHelpers.NewInMemoryDb();
        var caseId = Guid.NewGuid();
        var svc = Substitute.For<ITechLogCollectionService>();
        svc.InstallAsync(
                Arg.Any<string>(), Arg.Any<TechLogScenario>(), Arg.Any<string?>(), Arg.Any<CancellationToken>(),
                Arg.Any<Guid?>(), Arg.Any<Guid?>(), Arg.Any<long?>())
            .Returns(new TechLogStartResult(TechLogStartOutcome.Started, caseId));
        db.Investigations.Add(NewCase(caseId, InvestigationStatus.Collecting));
        await db.SaveChangesAsync();

        var result = await InvestigationEndpoints.StartInvestigationAsync(
            new StartInvestigationRequest(InvestigationScenario.Locks, InfobaseId: null),
            svc, db, TestHelpers.NewHttpContext("admin"), CancellationToken.None);

        result.Result.Should().BeOfType<Created<InvestigationSummary>>();
        await svc.Received(1).InstallAsync(
            "admin", TechLogScenario.Locks, null, Arg.Any<CancellationToken>(), null, null, null);
    }

    [Fact]
    public async Task Start_with_unknown_infobase_returns_not_found_and_does_not_install()
    {
        using var db = TestHelpers.NewInMemoryDb();
        var svc = Substitute.For<ITechLogCollectionService>();

        var result = await InvestigationEndpoints.StartInvestigationAsync(
            new StartInvestigationRequest(InvestigationScenario.Locks, Guid.NewGuid()),
            svc, db, TestHelpers.NewHttpContext("admin"), CancellationToken.None);

        result.Result.Should().BeOfType<NotFound>();
        await svc.DidNotReceive().InstallAsync(
            Arg.Any<string>(), Arg.Any<TechLogScenario>(), Arg.Any<string?>(), Arg.Any<CancellationToken>(),
            Arg.Any<Guid?>(), Arg.Any<Guid?>(), Arg.Any<long?>());
    }

    [Fact]
    public async Task Start_when_already_active_returns_conflict_investigation_active()
    {
        using var db = TestHelpers.NewInMemoryDb();
        var svc = Substitute.For<ITechLogCollectionService>();
        svc.InstallAsync(
                Arg.Any<string>(), Arg.Any<TechLogScenario>(), Arg.Any<string?>(), Arg.Any<CancellationToken>(),
                Arg.Any<Guid?>(), Arg.Any<Guid?>(), Arg.Any<long?>())
            .Returns(new TechLogStartResult(TechLogStartOutcome.AlreadyActive, Guid.NewGuid()));

        var result = await InvestigationEndpoints.StartInvestigationAsync(
            new StartInvestigationRequest(InvestigationScenario.Locks, InfobaseId: null),
            svc, db, TestHelpers.NewHttpContext("admin"), CancellationToken.None);

        var conflict = result.Result.Should().BeOfType<Conflict<ProblemDetails>>().Subject;
        conflict.Value!.Extensions["code"].Should().Be(ProblemCodes.InvestigationActive);
    }

    [Fact]
    public async Task Start_when_no_write_access_returns_start_failed_with_grant_command()
    {
        using var db = TestHelpers.NewInMemoryDb();
        var svc = Substitute.For<ITechLogCollectionService>();
        svc.InstallAsync(
                Arg.Any<string>(), Arg.Any<TechLogScenario>(), Arg.Any<string?>(), Arg.Any<CancellationToken>(),
                Arg.Any<Guid?>(), Arg.Any<Guid?>(), Arg.Any<long?>())
            .Returns(new TechLogStartResult(
                TechLogStartOutcome.NoWriteAccess, Guid.Empty,
                GrantCommand: "icacls \"C:\\conf\" /grant ...", Issue: "Нет прав на logcfg.xml."));

        var result = await InvestigationEndpoints.StartInvestigationAsync(
            new StartInvestigationRequest(InvestigationScenario.Locks, InfobaseId: null),
            svc, db, TestHelpers.NewHttpContext("admin"), CancellationToken.None);

        var conflict = result.Result.Should().BeOfType<Conflict<ProblemDetails>>().Subject;
        conflict.Value!.Extensions["code"].Should().Be(ProblemCodes.InvestigationStartFailed);
        conflict.Value!.Detail.Should().Contain("icacls", "точная команда выдачи прав уходит оператору");
    }

    // ── Стоп ────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Stop_returns_ok_summary()
    {
        using var db = TestHelpers.NewInMemoryDb();
        var id = Guid.NewGuid();
        var svc = Substitute.For<ITechLogCollectionService>();
        svc.RemoveAsync(id, InvestigationStopReason.Manual, Arg.Any<CancellationToken>())
            .Returns(TechLogStopOutcome.Stopped);
        db.Investigations.Add(NewCase(id, InvestigationStatus.Completed, InvestigationStopReason.Manual));
        await db.SaveChangesAsync();

        var result = await InvestigationEndpoints.StopInvestigationAsync(id, svc, db, CancellationToken.None);

        var ok = result.Result.Should().BeOfType<Ok<InvestigationSummary>>().Subject;
        ok.Value!.Status.Should().Be(InvestigationStatus.Completed);
        ok.Value!.StopReason.Should().Be(InvestigationStopReason.Manual);
    }

    [Fact]
    public async Task Stop_when_not_active_returns_conflict()
    {
        using var db = TestHelpers.NewInMemoryDb();
        var svc = Substitute.For<ITechLogCollectionService>();
        svc.RemoveAsync(Arg.Any<Guid>(), Arg.Any<InvestigationStopReason>(), Arg.Any<CancellationToken>())
            .Returns(TechLogStopOutcome.NotActive);

        var result = await InvestigationEndpoints.StopInvestigationAsync(
            Guid.NewGuid(), svc, db, CancellationToken.None);

        var conflict = result.Result.Should().BeOfType<Conflict<ProblemDetails>>().Subject;
        conflict.Value!.Extensions["code"].Should().Be(ProblemCodes.InvestigationNotActive);
    }

    // ── Список / деталь / отчёт / прогресс ────────────────────────────────────────────────────────

    [Fact]
    public async Task List_returns_summaries_newest_first_paged()
    {
        using var db = TestHelpers.NewInMemoryDb();
        var older = Guid.NewGuid();
        var newer = Guid.NewGuid();
        db.Investigations.Add(NewCase(older, InvestigationStatus.Completed, InvestigationStopReason.TimeLimit, T0));
        db.Investigations.Add(NewCase(newer, InvestigationStatus.Collecting, null, T0.AddMinutes(5)));
        await db.SaveChangesAsync();

        var result = await InvestigationEndpoints.ListInvestigationsAsync(
            page: null, pageSize: null, db, CancellationToken.None);

        result.Value!.Total.Should().Be(2);
        result.Value!.Items[0].Id.Should().Be(newer, "свежие сверху");
        result.Value!.Items[1].Id.Should().Be(older);
    }

    [Fact]
    public async Task Detail_returns_findings_with_result_as_object_and_config_snapshot()
    {
        using var db = TestHelpers.NewInMemoryDb();
        var id = Guid.NewGuid();
        var c = NewCase(id, InvestigationStatus.Completed, InvestigationStopReason.Manual);
        c.CollectionConfig = new CollectionConfig
        {
            LogcfgLocation = @"C:\techlog",
            Events = "DBMSSQL,SDBL",
            DurationThresholdMicros = 5_000_000,
            ProcessNameFilter = "infobase01",
            Format = "json",
            HistoryHours = 2,
        };
        db.Investigations.Add(c);
        db.Findings.Add(new Finding
        {
            Id = Guid.NewGuid(),
            InvestigationId = id,
            Kind = FindingKind.SlowQueries,
            SchemaVersion = 1,
            ResultJson = """{"topQueries":[{"durationSeconds":6.0}],"totalDbmssqlEvents":3}""",
        });
        await db.SaveChangesAsync();

        var result = await InvestigationEndpoints.GetInvestigationAsync(id, db, CancellationToken.None);

        var detail = result.Result.Should().BeOfType<Ok<InvestigationDetail>>().Subject.Value!;
        detail.Summary.Id.Should().Be(id);
        detail.CollectionConfig!.Events.Should().Be("DBMSSQL,SDBL");
        detail.Findings.Should().ContainSingle();
        var finding = detail.Findings[0];
        finding.Kind.Should().Be(FindingKind.SlowQueries);
        // result — вложенный JSON-ОБЪЕКТ (не строка): на проводе доступны поля анализатора.
        finding.Result.ValueKind.Should().Be(JsonValueKind.Object);
        finding.Result.GetProperty("totalDbmssqlEvents").GetInt32().Should().Be(3);
        finding.Result.GetProperty("topQueries").GetArrayLength().Should().Be(1);
    }

    [Fact]
    public async Task Detail_missing_returns_not_found()
    {
        using var db = TestHelpers.NewInMemoryDb();
        var result = await InvestigationEndpoints.GetInvestigationAsync(Guid.NewGuid(), db, CancellationToken.None);
        result.Result.Should().BeOfType<NotFound>();
    }

    [Fact]
    public async Task Report_ranks_findings_by_severity()
    {
        using var db = TestHelpers.NewInMemoryDb();
        var id = Guid.NewGuid();
        db.Investigations.Add(NewCase(id, InvestigationStatus.Completed, InvestigationStopReason.Manual));
        // 10 записей → Warning; 0 записей → None.
        db.Findings.Add(new Finding
        {
            Id = Guid.NewGuid(),
            InvestigationId = id,
            Kind = FindingKind.SlowQueries,
            SchemaVersion = 1,
            ResultJson = """{"topQueries":[1,2,3,4,5,6,7,8,9,10]}""",
        });
        db.Findings.Add(new Finding
        {
            Id = Guid.NewGuid(),
            InvestigationId = id,
            Kind = FindingKind.Exceptions,
            SchemaVersion = 1,
            ResultJson = """{"topExceptions":[]}""",
        });
        await db.SaveChangesAsync();

        var result = await InvestigationEndpoints.GetReportAsync(
            id, db, TestHelpers.FixedClock(T0), CancellationToken.None);

        var report = result.Result.Should().BeOfType<Ok<InvestigationReport>>().Subject.Value!;
        report.Items.Should().HaveCount(2);
        report.Items[0].Kind.Should().Be(FindingKind.SlowQueries, "Warning ранжируется выше None");
        report.Items[0].Severity.Should().Be(ReportSeverity.Warning);
        report.Items[0].Count.Should().Be(10);
        report.Items[1].Severity.Should().Be(ReportSeverity.None);
    }

    [Fact]
    public async Task Progress_for_active_case_reports_collected_size_via_store()
    {
        using var db = TestHelpers.NewInMemoryDb();
        var id = Guid.NewGuid();
        var c = NewCase(id, InvestigationStatus.Collecting, null, T0);
        db.Investigations.Add(c);
        await db.SaveChangesAsync();

        var store = Substitute.For<ILogcfgStore>();
        store.GetDirectorySizeBytes(@"C:\techlog").Returns(4096L);

        var result = await InvestigationEndpoints.GetProgressAsync(
            id, db, store, TestHelpers.FixedClock(T0.AddSeconds(30)), CancellationToken.None);

        var progress = result.Result.Should().BeOfType<Ok<InvestigationProgress>>().Subject.Value!;
        progress.Status.Should().Be(InvestigationStatus.Collecting);
        progress.ElapsedSeconds.Should().Be(30);
        progress.CollectedBytes.Should().Be(4096);
    }

    [Fact]
    public async Task Progress_for_completed_case_does_not_read_directory_size()
    {
        using var db = TestHelpers.NewInMemoryDb();
        var id = Guid.NewGuid();
        var c = NewCase(id, InvestigationStatus.Completed, InvestigationStopReason.Manual, T0);
        c.StoppedAtUtc = T0.AddMinutes(2);
        db.Investigations.Add(c);
        await db.SaveChangesAsync();

        var store = Substitute.For<ILogcfgStore>();

        var result = await InvestigationEndpoints.GetProgressAsync(
            id, db, store, TestHelpers.FixedClock(T0.AddMinutes(10)), CancellationToken.None);

        var progress = result.Result.Should().BeOfType<Ok<InvestigationProgress>>().Subject.Value!;
        progress.CollectedBytes.Should().BeNull("каталог снят после завершения — размер не читаем");
        progress.ElapsedSeconds.Should().Be(120, "прошедшее время = окно сбора завершённого дела");
        store.DidNotReceive().GetDirectorySizeBytes(Arg.Any<string>());
    }

    // ── Удаление ─────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Delete_removes_completed_case_and_audits()
    {
        using var db = TestHelpers.NewInMemoryDb();
        var id = Guid.NewGuid();
        db.Investigations.Add(NewCase(id, InvestigationStatus.Completed, InvestigationStopReason.Manual));
        await db.SaveChangesAsync();
        var audit = new TestHelpers.CapturingAuditLogger();

        var result = await InvestigationEndpoints.DeleteInvestigationAsync(
            id, db, audit, TestHelpers.NewHttpContext("admin"), CancellationToken.None);

        result.Result.Should().BeOfType<NoContent>();
        db.Investigations.Count().Should().Be(0);

        var entry = audit.Entries.Should().ContainSingle().Subject;
        entry.Action.Should().Be(AuditActionType.InvestigationDeleted);
        entry.Initiator.Should().Be("admin");
        entry.TenantId.Should().BeNull();
        entry.Description.Should().Contain(id.ToString("N"));
    }

    [Fact]
    public async Task Delete_active_case_returns_conflict()
    {
        using var db = TestHelpers.NewInMemoryDb();
        var id = Guid.NewGuid();
        db.Investigations.Add(NewCase(id, InvestigationStatus.Collecting));
        await db.SaveChangesAsync();
        var audit = new TestHelpers.CapturingAuditLogger();

        var result = await InvestigationEndpoints.DeleteInvestigationAsync(
            id, db, audit, TestHelpers.NewHttpContext("admin"), CancellationToken.None);

        var conflict = result.Result.Should().BeOfType<Conflict<ProblemDetails>>().Subject;
        conflict.Value!.Extensions["code"].Should().Be(ProblemCodes.InvestigationActive);
        db.Investigations.Count().Should().Be(1, "активное дело удалять нельзя — сначала остановить");
        audit.Entries.Should().BeEmpty();
    }

    [Fact]
    public async Task Delete_missing_returns_not_found()
    {
        using var db = TestHelpers.NewInMemoryDb();
        var audit = new TestHelpers.CapturingAuditLogger();

        var result = await InvestigationEndpoints.DeleteInvestigationAsync(
            Guid.NewGuid(), db, audit, TestHelpers.NewHttpContext("admin"), CancellationToken.None);

        result.Result.Should().BeOfType<NotFound>();
        audit.Entries.Should().BeEmpty();
    }

    private static Investigation NewCase(
        Guid id,
        InvestigationStatus status,
        InvestigationStopReason? stopReason = null,
        DateTime? startedAt = null) => new()
        {
            Id = id,
            Scenario = InvestigationScenario.Locks,
            Status = status,
            StartedAtUtc = startedAt ?? T0,
            StartedBy = "admin",
            StopReason = stopReason,
            CollectionDirectory = @"C:\techlog",
            ConfigMarker = "marker",
        };
}
