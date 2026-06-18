using Microsoft.Extensions.Logging;
using MitLicenseCenter.Application.Discovery;
using MitLicenseCenter.Application.Server;
using MitLicenseCenter.Infrastructure.Ras;

namespace MitLicenseCenter.Infrastructure.Server;

// Статус локальной службы SQL для агрегатора (MLC-213, только наблюдение, ADR-54).
// Обнаружение службы — по ImagePath, содержащему sqlservr.exe (через IServiceRegistryReader,
// один проход без спавнов — как RAS/ragent), состояние — через IServiceStateReader; имя
// инстанса — best-effort из ISqlInstanceDiscovery (первый локальный инстанс). NEVER-THROWS:
// любой сбой (реестр недоступен / ServiceController / discovery инстанса) → Available:false +
// санитизированный Error, а не исключение (вызывается из never-падающего провайдера).
internal sealed partial class SqlServiceStatusReader : ISqlServiceStatusReader
{
    private readonly IServiceRegistryReader _registry;
    private readonly IServiceStateReader _serviceState;
    private readonly ISqlInstanceDiscovery _instances;
    private readonly ILogger<SqlServiceStatusReader> _logger;

    public SqlServiceStatusReader(
        IServiceRegistryReader registry,
        IServiceStateReader serviceState,
        ISqlInstanceDiscovery instances,
        ILogger<SqlServiceStatusReader> logger)
    {
        _registry = registry;
        _serviceState = serviceState;
        _instances = instances;
        _logger = logger;
    }

    public SqlStatusSummary Read()
    {
        // Имя инстанса — best-effort и не критично: если discovery упал, продолжаем без него.
        string? instance = null;
        try
        {
            var instances = _instances.FindLocalInstances();
            instance = instances.Count > 0 ? instances[0] : null;
        }
        catch (Exception ex)
        {
            LogInstanceDiscoveryFailed(_logger, ex);
        }

        try
        {
            foreach (var svc in _registry.ReadServices())
            {
                if (!ReferencesSqlServer(svc.ImagePath))
                {
                    continue;
                }

                var state = _serviceState.ReadState(svc.Name);
                return new SqlStatusSummary(
                    Instance: instance,
                    ServiceName: svc.Name,
                    Running: state?.IsRunning ?? false,
                    Available: true,
                    Error: null);
            }

            // Служба SQL на узле не найдена — это доступный, но «нет службы» статус
            // (не ошибка адаптера): Available:true, ServiceName=null, Running=false.
            return new SqlStatusSummary(
                Instance: instance,
                ServiceName: null,
                Running: false,
                Available: true,
                Error: null);
        }
        catch (Exception ex)
        {
            LogSqlStatusFailed(_logger, ex);
            return new SqlStatusSummary(
                Instance: instance,
                ServiceName: null,
                Running: false,
                Available: false,
                Error: "Не удалось получить состояние службы SQL Server. Проверьте доступность СУБД и права службы.");
        }
    }

    // Служба ядра SQL Server — её ImagePath содержит sqlservr.exe (и для дефолтного, и
    // для именованных инстансов: ...\Binn\sqlservr.exe -sMSSQLSERVER / -sSQLEXPRESS).
    private static bool ReferencesSqlServer(string? imagePath)
        => !string.IsNullOrEmpty(imagePath)
           && imagePath.Contains("sqlservr.exe", StringComparison.OrdinalIgnoreCase);

    [LoggerMessage(
        Level = LogLevel.Debug,
        Message = "SQL-статус: не удалось получить имя локального инстанса (best-effort).")]
    private static partial void LogInstanceDiscoveryFailed(ILogger logger, Exception ex);

    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "SQL-статус: не удалось получить состояние службы SQL Server.")]
    private static partial void LogSqlStatusFailed(ILogger logger, Exception ex);
}
