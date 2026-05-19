namespace MitLicenseCenter.Web.Endpoints;

public sealed record CircuitStatusResponse(
    string State,
    DateTime LastTransitionAt,
    string? LastErrorMessage,
    string ActiveAdapter);
