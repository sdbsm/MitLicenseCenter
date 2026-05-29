using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MitLicenseCenter.Application.Clusters;

namespace MitLicenseCenter.Infrastructure.Clusters;

// Независимый 30s ping-loop. Обновляет RasHealthState; Dashboard читает snapshot.
// Независим от ReconciliationJob — health отвечает на «можем ли мы сейчас
// поговорить с RAS?», что отдельный вопрос от «свеж ли snapshot». Lifetime
// сервиса = lifetime приложения; cancellation = host shutdown.
//
// Замечание про scope (R4 плана): сейчас IClusterClient зарегистрирован как
// scoped, поэтому ping'аем внутри CreateScope(). Если будущий рефакторинг
// продвинет RAS-адаптер до singleton, ping-loop можно перевести на прямой
// constructor injection.
internal sealed partial class RasHealthProbingService : BackgroundService
{
    private static readonly TimeSpan ProbeInterval = TimeSpan.FromSeconds(30);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly RasHealthState _state;
    private readonly ILogger<RasHealthProbingService> _logger;

    public RasHealthProbingService(
        IServiceScopeFactory scopeFactory,
        RasHealthState state,
        ILogger<RasHealthProbingService> logger)
    {
        _scopeFactory = scopeFactory;
        _state = state;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var cluster = scope.ServiceProvider.GetRequiredService<IClusterClient>();
                var result = await cluster.PingAsync(stoppingToken).ConfigureAwait(false);
                if (result.Ok)
                {
                    _state.RecordSuccess();
                }
                else
                {
                    _state.RecordFailure(result.Error ?? "rac.exe вернул Ok=false без сообщения об ошибке.");
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                LogProbeFailed(_logger, ex);
                _state.RecordFailure(ex.Message);
            }

            try
            {
                await Task.Delay(ProbeInterval, stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Ошибка при проверке доступности RAS.")]
    private static partial void LogProbeFailed(ILogger logger, Exception exception);
}
