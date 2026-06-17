import { describe, expect, it } from "vitest";
import type { InfobaseListItem } from "@/features/infobases/types";
import { toPublicationListItem } from "../types";

function infobase(overrides: Partial<InfobaseListItem["publication"]> = {}): InfobaseListItem {
  return {
    id: "ib-1",
    tenantId: "t-1",
    tenantName: "ООО Ромашка",
    name: "Бухгалтерия",
    clusterInfobaseId: "11111111-1111-1111-1111-111111111111",
    databaseName: "acme_bp",
    status: "Active",
    createdAt: "2026-06-01T00:00:00Z",
    updatedAt: null,
    rowVersion: null,
    currentDataBytes: null,
    currentLogBytes: null,
    publication: {
      id: "pub-1",
      infobaseId: "ib-1",
      siteName: "Default Web Site",
      virtualPath: "/acme-bp",
      platformVersion: "8.3.23.1865",
      source: "Webinst",
      physicalPathOverride: null,
      createdAt: "2026-06-01T00:00:00Z",
      updatedAt: null,
      lastCheckStatus: "Published",
      lastCheckAt: "2026-06-10T08:00:00Z",
      lastCheckDetails: null,
      rowVersion: null,
      ...overrides,
    },
  };
}

// MLC-081: после слияния страниц диалоги публикации и bulk-операции получают строки
// через этот маппинг — фиксируем соответствие полей строке списка инфобаз.
describe("toPublicationListItem", () => {
  it("maps infobase list item to flat publication row", () => {
    const row = toPublicationListItem(infobase());
    expect(row).toEqual({
      id: "pub-1",
      infobaseId: "ib-1",
      infobaseName: "Бухгалтерия",
      tenantId: "t-1",
      tenantName: "ООО Ромашка",
      siteName: "Default Web Site",
      virtualPath: "/acme-bp",
      platformVersion: "8.3.23.1865",
      source: "Webinst",
      lastCheckStatus: "Published",
      lastCheckAt: "2026-06-10T08:00:00Z",
      lastCheckDetails: null,
    });
  });

  it("normalizes omitted nullable fields to null (API опускает null-поля)", () => {
    const row = toPublicationListItem(
      infobase({ lastCheckAt: undefined, lastCheckDetails: undefined })
    );
    expect(row.lastCheckAt).toBeNull();
    expect(row.lastCheckDetails).toBeNull();
  });
});
