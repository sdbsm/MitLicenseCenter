namespace MitLicenseCenter.Application.Clusters;

// Источник snapshot'а здоровья RAS для Dashboard. Пишется
// RasHealthProbingService (BackgroundService, 30s ping cadence), читается
// DashboardEndpoints.ComputeAsync. Аудит-нейтрален — частые transient blips
// не должны полировать AuditLog; оператор видит реальное состояние на карточке.
public interface IRasHealthReader
{
    RasHealthSnapshot GetSnapshot();
}

public sealed record RasHealthSnapshot(
    bool Healthy,
    DateTime? LastCheckedAtUtc,
    string? LastErrorMessage,
    int ConsecutiveFailures);
