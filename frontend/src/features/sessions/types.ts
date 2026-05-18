export interface SessionSnapshotEntry {
  sessionId: string;
  infobaseId: string;
  tenantId: string;
  appId: string;
  consumesLicense: boolean;
  startedAt: string;
}

export interface SessionsSnapshotResponse {
  items: SessionSnapshotEntry[];
  capturedAt: string;
  tookMs: number;
}
