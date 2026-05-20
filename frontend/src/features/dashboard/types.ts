export type ClusterCircuitState = "Closed" | "Open" | "HalfOpen";
export type ClusterAdapter = "Rest" | "Ras";

export interface DashboardClusterStatus {
  state: ClusterCircuitState;
  lastTransitionAt: string;
  lastErrorMessage: string | null;
  activeAdapter: ClusterAdapter;
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
  cluster: DashboardClusterStatus;
}
