namespace MitLicenseCenter.Web.Endpoints;

public sealed record DashboardSummaryResponse(
    int TenantsTotal,
    int TenantsActive,
    int InfobasesTotal,
    int SessionsActiveTotal,
    int LicensesConsumedTotal,
    int LicensesAvailableTotal,
    IReadOnlyList<TenantConsumptionRow> TopTenantsByConsumption,
    DashboardClusterStatus Cluster);

public sealed record TenantConsumptionRow(
    Guid TenantId,
    string TenantName,
    int Consumed,
    int Limit,
    int Percent);

public sealed record DashboardClusterStatus(
    string State,
    DateTime LastTransitionAt,
    string? LastErrorMessage,
    string ActiveAdapter);
