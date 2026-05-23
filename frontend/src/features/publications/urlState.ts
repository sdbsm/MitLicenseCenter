import type { PublicationDriftStatus } from "./types";

export const DRIFT_STATUSES: readonly PublicationDriftStatus[] = [
  "InSync",
  "Drift",
  "Missing",
  "Error",
];

export interface UrlFilters {
  tenantId: string;
  driftStatus: PublicationDriftStatus | "";
}

export function isDriftStatus(value: string): value is PublicationDriftStatus {
  return (DRIFT_STATUSES as readonly string[]).includes(value);
}

export function parseParams(params: URLSearchParams): UrlFilters {
  const ds = params.get("driftStatus") ?? "";
  return {
    tenantId: params.get("tenantId") ?? "",
    driftStatus: isDriftStatus(ds) ? ds : "",
  };
}
