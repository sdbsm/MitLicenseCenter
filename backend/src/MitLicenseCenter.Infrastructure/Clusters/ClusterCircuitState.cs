using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MitLicenseCenter.Application.Auditing;
using MitLicenseCenter.Application.Clusters;
using MitLicenseCenter.Application.Settings;
using MitLicenseCenter.Domain.Audit;
using MitLicenseCenter.Domain.Settings;
using Polly;
using Polly.CircuitBreaker;

namespace MitLicenseCenter.Infrastructure.Clusters;

// Singleton-хранилище состояния цепи + Polly ResiliencePipeline.
// Pipeline — singleton, потому что circuit-breaker должен помнить состояние
// между HTTP-запросами разных scope'ов. IAuditLogger — scoped (DbContext),
// поэтому пишем аудит через IServiceScopeFactory (паттерн как в SettingsSnapshot).
internal sealed class ClusterCircuitState : ICircuitStatusReader
{
    private const int DefaultMinimumThroughput = 3;
    private const int DefaultBreakDurationSeconds = 60;

    private readonly Lock _gate = new();
    private readonly ILogger<ClusterCircuitState> _logger;

    private string _state = "Closed";
    private DateTime _lastTransitionAtUtc = DateTime.UtcNow;
    private string? _lastErrorMessage;

    // Используется в тестах напрямую.
    internal ResiliencePipeline Pipeline { get; }

    public ClusterCircuitState(
        IServiceScopeFactory scopeFactory,
        ISettingsSnapshot settings,
        ILogger<ClusterCircuitState> logger)
        : this(
            scopeFactory,
            logger,
            minimumThroughput: settings.GetInt(SettingKey.CircuitBreakerFailureCount) ?? DefaultMinimumThroughput,
            breakDurationSeconds: settings.GetInt(SettingKey.CircuitBreakerProbeIntervalSeconds) ?? DefaultBreakDurationSeconds)
    {
    }

    // Конструктор для тестов: явные параметры вместо чтения из ISettingsSnapshot.
    internal ClusterCircuitState(
        IServiceScopeFactory scopeFactory,
        ILogger<ClusterCircuitState> logger,
        int minimumThroughput,
        int breakDurationSeconds)
    {
        _logger = logger;
        Pipeline = BuildPipeline(scopeFactory, minimumThroughput, breakDurationSeconds);
    }

    public CircuitStatus GetStatus()
    {
        lock (_gate)
        {
            return new CircuitStatus(
                State: _state,
                LastTransitionAtUtc: _lastTransitionAtUtc,
                LastErrorMessage: _lastErrorMessage,
                ActiveAdapter: _state == "Open" ? "Ras" : "Rest");
        }
    }

    internal void MarkOpen(string? error)
    {
        lock (_gate)
        {
            _state = "Open";
            _lastTransitionAtUtc = DateTime.UtcNow;
            _lastErrorMessage = error;
        }
    }

    internal void MarkClosed()
    {
        lock (_gate)
        {
            _state = "Closed";
            _lastTransitionAtUtc = DateTime.UtcNow;
            _lastErrorMessage = null;
        }
    }

    internal void MarkHalfOpen()
    {
        lock (_gate)
        {
            _state = "HalfOpen";
            _lastTransitionAtUtc = DateTime.UtcNow;
        }
    }

    private ResiliencePipeline BuildPipeline(
        IServiceScopeFactory scopeFactory,
        int minimumThroughput,
        int breakDurationSeconds)
    {
        return new ResiliencePipelineBuilder()
            .AddCircuitBreaker(new CircuitBreakerStrategyOptions
            {
                // 100% failures среди MinimumThroughput запросов → цепь размыкается.
                FailureRatio = 1.0,
                MinimumThroughput = minimumThroughput,
                SamplingDuration = TimeSpan.FromSeconds(30),
                BreakDuration = TimeSpan.FromSeconds(breakDurationSeconds),
                ShouldHandle = new PredicateBuilder().Handle<Exception>(),
                OnOpened = async args =>
                {
                    var error = args.Outcome.Exception?.Message ?? "неизвестная ошибка";
                    MarkOpen(error);
                    await WriteAuditAsync(
                        scopeFactory,
                        AuditActionType.ClusterAdapterCircuitOpened,
                        $"Цепь к 1С Cluster REST API разомкнута: {error}.").ConfigureAwait(false);
                },
                OnClosed = async _ =>
                {
                    MarkClosed();
                    await WriteAuditAsync(
                        scopeFactory,
                        AuditActionType.ClusterAdapterCircuitClosed,
                        "Цепь к 1С Cluster REST API замкнута.").ConfigureAwait(false);
                },
                OnHalfOpened = _ =>
                {
                    MarkHalfOpen();
                    return ValueTask.CompletedTask;
                },
            })
            .Build();
    }

    private async ValueTask WriteAuditAsync(
        IServiceScopeFactory scopeFactory,
        AuditActionType action,
        string description)
    {
        try
        {
            using var scope = scopeFactory.CreateScope();
            var audit = scope.ServiceProvider.GetRequiredService<IAuditLogger>();
            await audit.LogAsync(action, initiator: "System", description: description)
                .ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Не удалось записать аудит circuit-breaker (action={Action}).", action);
        }
    }
}
