using System.Reflection;
using Asp.Versioning;
using Asp.Versioning.Builder;
using Hangfire;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MitLicenseCenter.Application.Clusters;
using MitLicenseCenter.Infrastructure.Persistence;

namespace MitLicenseCenter.Web.Endpoints;

public static partial class HealthEndpoints
{
    // MLC-040 (PERF-04): пробы read-only и дёшевы, но под общим таймаутом — ждать
    // зависимость, которая «не отвечает», для readiness смысла нет (это уже «down»).
    private static readonly TimeSpan ReadinessProbeTimeout = TimeSpan.FromSeconds(2);

    public static void MapHealthEndpoints(this IEndpointRouteBuilder endpoints, ApiVersionSet versionSet)
    {
        var version = Assembly
            .GetExecutingAssembly()
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
            .InformationalVersion ?? "0.0.0";

        // Liveness — намеренно дёшев: процесс отвечает ⇒ жив. Никаких зависимостей,
        // прод-поведение 1:1 (контракт уже могут дёргать мониторинг/оркестратор).
        endpoints
            .MapGet("/api/v{version:apiVersion}/health", () => Results.Ok(new
            {
                status = "ok",
                version,
                utcNow = DateTime.UtcNow,
            }))
            .WithApiVersionSet(versionSet)
            .HasApiVersion(new ApiVersion(1, 0))
            .WithTags("Health")
            .AllowAnonymous();

        // Readiness (MLC-040) — проба готовности зависимостей. Анонимный, как liveness
        // (нужен probe-инструментам без аутентификации), но санитизированный: тело несёт
        // только грубые суб-статусы, без путей/имён серверов/текстов исключений.
        endpoints
            .MapGet("/api/v{version:apiVersion}/health/ready", ReadyAsync)
            .WithApiVersionSet(versionSet)
            .HasApiVersion(new ApiVersion(1, 0))
            .WithTags("Health")
            .AllowAnonymous();
    }

    internal static async Task<Results<Ok<ReadinessResponse>, JsonHttpResult<ReadinessResponse>>> ReadyAsync(
        AppDbContext db,
        IRasHealthReader rasHealth,
        JobStorage hangfireStorage,
        ILoggerFactory loggerFactory,
        CancellationToken ct)
    {
        var logger = loggerFactory.CreateLogger(typeof(HealthEndpoints).FullName!);

        var database = await ProbeDatabaseAsync(db, logger, ct).ConfigureAwait(false);
        var ras = ProbeRas(rasHealth);
        var hangfire = await ProbeHangfireAsync(hangfireStorage, logger, ct).ConfigureAwait(false);

        var (overall, httpStatus) = ReadinessEvaluator.Evaluate(database, ras, hangfire);
        var body = new ReadinessResponse(overall, DateTime.UtcNow, new ReadinessChecks(database, ras, hangfire));

        return httpStatus == StatusCodes.Status200OK
            ? TypedResults.Ok(body)
            : TypedResults.Json(body, statusCode: httpStatus);
    }

    // БД — единственная зависимость, гейтящая not_ready/503. CanConnectAsync дёшев
    // (открыть соединение), но под таймаутом: повисший SQL не должен вешать пробу.
    private static async Task<string> ProbeDatabaseAsync(AppDbContext db, ILogger logger, CancellationToken ct)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(ReadinessProbeTimeout);
        try
        {
            return await db.Database.CanConnectAsync(cts.Token).ConfigureAwait(false)
                ? ReadinessStatus.Ok
                : ReadinessStatus.Down;
        }
        // Отмену самого запроса (клиент ушёл) пробрасываем; таймаут пробы и ошибки
        // соединения — наружу только «down», полное исключение в журнал сервера.
        catch (Exception ex) when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
        {
            LogDatabaseProbeFailed(logger, ex);
            return ReadinessStatus.Down;
        }
    }

    // RAS — чистое чтение снапшота 30с-пробера (RasHealthProbingService). БЕЗ нового
    // спавна rac.exe (ADR-16 / 3.3, спавн-бюджет): GetSnapshot() читает память под lock.
    private static string ProbeRas(IRasHealthReader rasHealth)
    {
        var snapshot = rasHealth.GetSnapshot();
        if (snapshot.LastCheckedAtUtc is null)
        {
            return ReadinessStatus.Unknown; // первые 30с после старта, ещё не пробовали
        }

        return snapshot.Healthy ? ReadinessStatus.Ok : ReadinessStatus.Degraded;
    }

    // Hangfire-сторадж — GetStatistics() синхронен и ходит в SQL; уводим в пул и
    // ограничиваем таймаутом через WhenAny, чтобы повисший сторадж не вешал ответ.
    private static async Task<string> ProbeHangfireAsync(JobStorage storage, ILogger logger, CancellationToken ct)
    {
        try
        {
            var work = Task.Run(() => storage.GetMonitoringApi().GetStatistics());
            var finished = await Task.WhenAny(work, Task.Delay(ReadinessProbeTimeout, ct)).ConfigureAwait(false);
            if (finished != work)
            {
                ct.ThrowIfCancellationRequested(); // клиент ушёл — пробрасываем отмену
                LogHangfireProbeFailed(logger, new TimeoutException("Проба Hangfire-стораджа превысила таймаут."));
                return ReadinessStatus.Down;
            }

            await work.ConfigureAwait(false); // пробросит исключение пробы, если было
            return ReadinessStatus.Ok;
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
        {
            LogHangfireProbeFailed(logger, ex);
            return ReadinessStatus.Down;
        }
    }
}

// MLC-040 / MLC-009: полное исключение пробы пишем в журнал сервера (source-gen logger,
// как в Discovery); наружу анонимному вызову уходит только грубый суб-статус.
public static partial class HealthEndpoints
{
    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "Readiness: проба БД не удалась (CanConnect).")]
    private static partial void LogDatabaseProbeFailed(ILogger logger, Exception ex);

    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "Readiness: проба Hangfire-стораджа не удалась.")]
    private static partial void LogHangfireProbeFailed(ILogger logger, Exception ex);
}
