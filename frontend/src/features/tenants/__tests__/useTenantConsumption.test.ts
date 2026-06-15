import { describe, expect, it } from "vitest";
import { buildConsumedByTenant } from "../useTenantConsumption";
import type { LicenseStatus, SessionSnapshotEntry } from "@/features/sessions/types";

// ADR-48 (MLC-166): consumes bool отображается на licenseStatus.
function makeEntry(
  tenantId: string,
  consumes: boolean,
  sessionId = Math.random().toString(),
  status?: LicenseStatus
): SessionSnapshotEntry {
  return {
    sessionId,
    clusterInfobaseId: "cluster-1",
    tenantId,
    tenantName: "Tenant " + tenantId,
    infobaseName: "Base",
    appId: "1cv8",
    userName: "user",
    host: "host",
    licenseStatus: status ?? (consumes ? "Consuming" : "NotConsuming"),
    startedAt: "2026-01-01T00:00:00Z",
    durationSeconds: 60,
  };
}

describe("buildConsumedByTenant", () => {
  it("returns empty map for empty input", () => {
    const result = buildConsumedByTenant([]);
    expect(result.size).toBe(0);
  });

  it("counts only licenseStatus===Consuming entries", () => {
    const items = [
      makeEntry("t1", true),
      makeEntry("t1", false), // NotConsuming — не считается
      makeEntry("t1", true),
    ];
    const result = buildConsumedByTenant(items);
    expect(result.get("t1")).toBe(2);
  });

  it("excludes Pending sessions from the count (ADR-48)", () => {
    const items = [
      makeEntry("t1", true),
      makeEntry("t1", false, undefined, "Pending"), // определяется — не считается
    ];
    const result = buildConsumedByTenant(items);
    expect(result.get("t1")).toBe(1);
  });

  it("groups by tenantId correctly", () => {
    const items = [
      makeEntry("t1", true),
      makeEntry("t2", true),
      makeEntry("t2", true),
      makeEntry("t3", false), // не считается
    ];
    const result = buildConsumedByTenant(items);
    expect(result.get("t1")).toBe(1);
    expect(result.get("t2")).toBe(2);
    expect(result.get("t3")).toBeUndefined();
  });

  it("does not include tenant with only non-license sessions", () => {
    const items = [makeEntry("t1", false), makeEntry("t1", false)];
    const result = buildConsumedByTenant(items);
    expect(result.has("t1")).toBe(false);
  });

  it("returns 0 for missing tenantId (Map.get returns undefined)", () => {
    const result = buildConsumedByTenant([makeEntry("t1", true)]);
    // Клиент без сеансов в снапшоте → consumed = 0 (Map.get → undefined → ?? 0)
    expect(result.get("t_missing") ?? 0).toBe(0);
  });
});
