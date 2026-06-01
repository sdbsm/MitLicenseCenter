import { describe, expect, it } from "vitest";
import { groupByTenant } from "../grouping";
import type { InfobaseListItem } from "../types";

function ib(id: string, tenantId: string, tenantName: string, name: string): InfobaseListItem {
  return {
    id,
    tenantId,
    tenantName,
    name,
    clusterInfobaseId: id,
    databaseServer: "sql.local",
    databaseName: name,
    status: "Active",
    createdAt: "2026-01-01T00:00:00Z",
    updatedAt: null,
    publication: {
      id: `pub-${id}`,
      infobaseId: id,
      siteName: "Default Web Site",
      virtualPath: `/${name}`,
      platformVersion: "8.3.23.1865",
      enableOData: false,
      enableHttpServices: false,
      vrdCustomXml: null,
      physicalPathOverride: null,
      createdAt: "2026-01-01T00:00:00Z",
      updatedAt: null,
    },
  };
}

describe("groupByTenant", () => {
  it("groups bases by tenant and sorts groups and items by name", () => {
    const items = [
      ib("1", "t-b", "Globex", "zup"),
      ib("2", "t-a", "Acme", "bp"),
      ib("3", "t-a", "Acme", "accounting"),
    ];
    const names = new Map([
      ["t-a", "Acme"],
      ["t-b", "Globex"],
    ]);

    const groups = groupByTenant(items, names);

    expect(groups.map((g) => g.tenantName)).toEqual(["Acme", "Globex"]);
    expect(groups[0].items.map((i) => i.name)).toEqual(["accounting", "bp"]);
    expect(groups[1].items).toHaveLength(1);
  });

  it("prefers the current tenant name over the stale one on the base", () => {
    const items = [ib("1", "t-a", "Old Name", "bp")];
    const groups = groupByTenant(items, new Map([["t-a", "New Name"]]));
    expect(groups[0].tenantName).toBe("New Name");
  });

  it("falls back to the base's tenantName when not in the map", () => {
    const items = [ib("1", "t-a", "Acme", "bp")];
    const groups = groupByTenant(items, new Map());
    expect(groups[0].tenantName).toBe("Acme");
  });
});
