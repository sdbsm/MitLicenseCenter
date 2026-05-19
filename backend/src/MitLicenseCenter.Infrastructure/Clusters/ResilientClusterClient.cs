using MitLicenseCenter.Application.Clusters;
using Polly.CircuitBreaker;

namespace MitLicenseCenter.Infrastructure.Clusters;

// Декоратор: пытается вызвать REST-primary (любой IClusterClient); при открытой цепи
// (BrokenCircuitException) переключается на RAS-fallback. Сам не пишет аудит —
// переходы цепи обрабатываются в ClusterCircuitState.OnOpened/OnClosed callbacks.
// primary инжектируется как IClusterClient (из фабричной регистрации в DI),
// что позволяет тестам подставить любой fake без HttpClient.
internal sealed class ResilientClusterClient : IClusterClient
{
    private readonly IClusterClient _primary;
    private readonly IRasFallbackClusterClient _fallback;
    private readonly ClusterCircuitState _state;

    public ResilientClusterClient(
        IClusterClient primary,
        IRasFallbackClusterClient fallback,
        ClusterCircuitState state)
    {
        _primary = primary;
        _fallback = fallback;
        _state = state;
    }

    public async Task<IReadOnlyList<ClusterSession>> ListActiveSessionsAsync(CancellationToken ct)
    {
        try
        {
            return await _state.Pipeline.ExecuteAsync<IReadOnlyList<ClusterSession>>(
                async token => await _primary.ListActiveSessionsAsync(token).ConfigureAwait(false),
                ct).ConfigureAwait(false);
        }
        catch (BrokenCircuitException)
        {
            return await _fallback.ListActiveSessionsAsync(ct).ConfigureAwait(false);
        }
    }

    public async Task<KillSessionResult> KillSessionAsync(SessionDescriptor descriptor, CancellationToken ct)
    {
        try
        {
            return await _state.Pipeline.ExecuteAsync<KillSessionResult>(
                async token => await _primary.KillSessionAsync(descriptor, token).ConfigureAwait(false),
                ct).ConfigureAwait(false);
        }
        catch (BrokenCircuitException)
        {
            return await _fallback.KillSessionAsync(descriptor, ct).ConfigureAwait(false);
        }
    }

    public async Task<ClusterPingResult> PingAsync(CancellationToken ct)
    {
        try
        {
            return await _state.Pipeline.ExecuteAsync<ClusterPingResult>(
                async token => await _primary.PingAsync(token).ConfigureAwait(false),
                ct).ConfigureAwait(false);
        }
        catch (BrokenCircuitException)
        {
            return await _fallback.PingAsync(ct).ConfigureAwait(false);
        }
    }
}
