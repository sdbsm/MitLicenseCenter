using Microsoft.Extensions.Logging;
using MitLicenseCenter.Application.Publishing;
using MitLicenseCenter.Application.Ras;
using MitLicenseCenter.Application.Server;

namespace MitLicenseCenter.Infrastructure.Server;

// Read-агрегатор статуса служб узла (MLC-213, ADR-54/55): композиция над обнаружением
// ragent, IRasServiceManager, статусом службы SQL и IIisLifecycleService. Каждый источник
// обёрнут в try/catch (КРОМЕ OperationCanceledException — пробрасываем): сбой одного
// адаптера → его Available:false/Error, а не падение всего снимка (паттерн discovery IIS,
// MLC-047). Overall — простая эвристика «светофора» для FE (MLC-214), см. ComputeOverall.
internal sealed partial class ServerStatusProvider : IServerStatusProvider
{
    private readonly IOneCServerDiscovery _oneCServers;
    private readonly IRasServiceManager _ras;
    private readonly ISqlServiceStatusReader _sql;
    private readonly IIisLifecycleService _iis;
    private readonly ILogger<ServerStatusProvider> _logger;

    public ServerStatusProvider(
        IOneCServerDiscovery oneCServers,
        IRasServiceManager ras,
        ISqlServiceStatusReader sql,
        IIisLifecycleService iis,
        ILogger<ServerStatusProvider> logger)
    {
        _oneCServers = oneCServers;
        _ras = ras;
        _sql = sql;
        _iis = iis;
        _logger = logger;
    }

    public async Task<ServerStatusSnapshot> GetStatusAsync(CancellationToken ct)
    {
        var oneCServers = DiscoverOneCServers();
        var ras = await DiagnoseRasAsync(ct).ConfigureAwait(false);
        var sql = ReadSql();
        var iis = await ReadIisAsync(ct).ConfigureAwait(false);

        var overall = ComputeOverall(oneCServers, ras, sql, iis);
        return new ServerStatusSnapshot(oneCServers, ras, sql, iis, overall);
    }

    // ── Источники (каждый деградирует независимо) ───────────────────────────────────────

    // Обнаружение ragent чисто синхронное (реестр). На сбое — пустой список (FE покажет
    // «сервер 1С не обнаружен»); это влияет на Overall (нет running ragent → Down/Unknown).
    private IReadOnlyList<OneCServerStatus> DiscoverOneCServers()
    {
        try
        {
            return _oneCServers.Discover();
        }
        catch (Exception ex)
        {
            LogOneCDiscoveryFailed(_logger, ex);
            return [];
        }
    }

    private async Task<RasStatusSummary> DiagnoseRasAsync(CancellationToken ct)
    {
        try
        {
            var d = await _ras.DiagnoseAsync(ct).ConfigureAwait(false);
            return new RasStatusSummary(
                State: d.State.ToString(),
                Running: d.Service?.IsRunning ?? false,
                ServiceName: d.Service?.ServiceName,
                Available: true,
                Error: null);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            LogRasDiagnosisFailed(_logger, ex);
            return new RasStatusSummary(
                State: "Unknown",
                Running: false,
                ServiceName: null,
                Available: false,
                Error: "Не удалось получить состояние службы RAS. Технические подробности записаны в журнал сервера.");
        }
    }

    // SqlServiceStatusReader сам never-throws, но оборачиваем для единообразия (на случай
    // неожиданного сбоя резолва зависимостей).
    private SqlStatusSummary ReadSql()
    {
        try
        {
            return _sql.Read();
        }
        catch (Exception ex)
        {
            LogSqlReadFailed(_logger, ex);
            return new SqlStatusSummary(
                Instance: null,
                ServiceName: null,
                Running: false,
                Available: false,
                Error: "Не удалось получить состояние службы SQL Server. Технические подробности записаны в журнал сервера.");
        }
    }

    private async Task<IisStatusSummary> ReadIisAsync(CancellationToken ct)
    {
        try
        {
            var state = await _iis.GetServerStateAsync(ct).ConfigureAwait(false);
            return new IisStatusSummary(state.ToString(), Available: true, Error: null);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            LogIisStatusFailed(_logger, ex);
            return new IisStatusSummary(
                "Unknown",
                Available: false,
                Error: "Не удалось получить состояние службы IIS (W3SVC). Проверьте доступность веб-сервера и права службы.");
        }
    }

    // ── Эвристика общего здоровья (светофор для FE, MLC-214) ────────────────────────────

    // Простые правила (документированы в docs/04_BACKEND.md §3.8):
    //   Unknown  — все четыре источника недоступны (опросить узел вообще не удалось);
    //   Down     — ни одной запущенной службы ragent (сервер 1С — ядро узла);
    //   Degraded — есть запущенный ragent, но что-то не так: RAS не Ok/не running, SQL не
    //              running, IIS не Started, либо любой адаптер Available:false;
    //   Healthy  — всё доступно и в норме.
    private static ServerHealth ComputeOverall(
        IReadOnlyList<OneCServerStatus> oneCServers,
        RasStatusSummary ras,
        SqlStatusSummary sql,
        IisStatusSummary iis)
    {
        // Вообще ничего не удалось опросить (ragent-обнаружение тоже сбоит → пустой список,
        // неотличимый от «нет служб», поэтому Unknown только когда ВСЕ адаптеры недоступны).
        var nothingAvailable = oneCServers.Count == 0 && !ras.Available && !sql.Available && !iis.Available;
        if (nothingAvailable)
        {
            return ServerHealth.Unknown;
        }

        var anyOneCRunning = oneCServers.Any(s => s.Running);
        if (!anyOneCRunning)
        {
            return ServerHealth.Down;
        }

        var anyAdapterUnavailable = !ras.Available || !sql.Available || !iis.Available;
        var rasUnhealthy = ras.Available && (!ras.Running || !string.Equals(ras.State, "Ok", StringComparison.Ordinal));
        var sqlUnhealthy = sql.Available && !sql.Running;
        var iisUnhealthy = iis.Available && !string.Equals(iis.State, "Started", StringComparison.Ordinal);

        if (anyAdapterUnavailable || rasUnhealthy || sqlUnhealthy || iisUnhealthy)
        {
            return ServerHealth.Degraded;
        }

        return ServerHealth.Healthy;
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Статус узла: обнаружение служб сервера 1С (ragent) не удалось.")]
    private static partial void LogOneCDiscoveryFailed(ILogger logger, Exception ex);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Статус узла: диагностика службы RAS не удалась.")]
    private static partial void LogRasDiagnosisFailed(ILogger logger, Exception ex);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Статус узла: чтение состояния службы SQL не удалось.")]
    private static partial void LogSqlReadFailed(ILogger logger, Exception ex);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Статус узла: чтение состояния службы IIS (W3SVC) не удалось.")]
    private static partial void LogIisStatusFailed(ILogger logger, Exception ex);
}
