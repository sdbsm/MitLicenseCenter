export interface SessionSnapshotEntry {
  sessionId: string;
  clusterInfobaseId: string;
  tenantId: string;
  tenantName: string;
  infobaseName: string;
  appId: string;
  userName: string;
  host: string;
  consumesLicense: boolean;
  startedAt: string;
  durationSeconds: number;
}

export interface SessionsSnapshotResponse {
  items: SessionSnapshotEntry[];
  capturedAt: string;
  tookMs: number;
  source: string;
}
