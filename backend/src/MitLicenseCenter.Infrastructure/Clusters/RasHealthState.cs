using MitLicenseCenter.Application.Clusters;

namespace MitLicenseCenter.Infrastructure.Clusters;

// Singleton publisher последнего результата ping'а RAS. Пишется
// RasHealthProbingService; читается DashboardEndpoints. Аудит-нейтрален в
// PR 5.1 (частые transient blips полировали бы audit-лог; оператор видит
// статус на Dashboard в реальном времени). Initial state = Healthy=true,
// LastCheckedAtUtc=null → frontend рендерит «Проверка…» до первого пробега.
internal sealed class RasHealthState : IRasHealthReader
{
    private readonly Lock _gate = new();
    private bool _healthy = true;
    private DateTime? _lastCheckedAtUtc;
    private string? _lastErrorMessage;
    private int _consecutiveFailures;

    public RasHealthSnapshot GetSnapshot()
    {
        lock (_gate)
        {
            return new RasHealthSnapshot(
                Healthy: _healthy,
                LastCheckedAtUtc: _lastCheckedAtUtc,
                LastErrorMessage: _lastErrorMessage,
                ConsecutiveFailures: _consecutiveFailures);
        }
    }

    internal void RecordSuccess()
    {
        lock (_gate)
        {
            _healthy = true;
            _lastCheckedAtUtc = DateTime.UtcNow;
            _lastErrorMessage = null;
            _consecutiveFailures = 0;
        }
    }

    internal void RecordFailure(string error)
    {
        lock (_gate)
        {
            _healthy = false;
            _lastCheckedAtUtc = DateTime.UtcNow;
            _lastErrorMessage = error;
            _consecutiveFailures++;
        }
    }
}
