namespace MitLicenseCenter.Application.Clusters;

public interface ICircuitStatusReader
{
    CircuitStatus GetStatus();
}

// State: "Closed" | "Open" | "HalfOpen"
// ActiveAdapter: "Rest" | "Ras"
public sealed record CircuitStatus(
    string State,
    DateTime LastTransitionAtUtc,
    string? LastErrorMessage,
    string ActiveAdapter);
