// Stage 5 PR 5.1 (ADR-16): REST cluster adapter + Polly circuit breaker удалены.
// Dashboard cluster card → RAS health card; backend publish'ит снапшот через
// IRasHealthReader + RasHealthProbingService (30s ping cadence).
export interface DashboardRasHealth {
  healthy: boolean;
  lastCheckedAtUtc: string | null;
  lastErrorMessage: string | null;
  consecutiveFailures: number;
}

export interface TenantConsumptionRow {
  tenantId: string;
  tenantName: string;
  consumed: number;
  limit: number;
  percent: number;
}

export interface DashboardSummaryResponse {
  tenantsTotal: number;
  tenantsActive: number;
  infobasesTotal: number;
  sessionsActiveTotal: number;
  licensesConsumedTotal: number;
  licensesAvailableTotal: number;
  topTenantsByConsumption: TenantConsumptionRow[];
  ras: DashboardRasHealth;
}
