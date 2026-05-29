namespace MitLicenseCenter.Web.Endpoints;

public sealed record DashboardSummaryResponse(
    int TenantsTotal,
    int TenantsActive,
    int InfobasesTotal,
    int SessionsActiveTotal,
    int LicensesConsumedTotal,
    int LicensesAvailableTotal,
    IReadOnlyList<TenantConsumptionRow> TopTenantsByConsumption,
    DashboardRasHealth Ras);

public sealed record TenantConsumptionRow(
    Guid TenantId,
    string TenantName,
    int Consumed,
    int Limit,
    int Percent);

// Stage 5 PR 5.1 (ADR-16): заменяет старый DashboardClusterStatus.
// LastCheckedAtUtc=null означает «первый ping ещё не отработал» — frontend
// рендерит «Проверка…» neutral badge.
public sealed record DashboardRasHealth(
    bool Healthy,
    DateTime? LastCheckedAtUtc,
    string? LastErrorMessage,
    int ConsecutiveFailures);
