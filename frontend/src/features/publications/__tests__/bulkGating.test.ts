import { describe, expect, it } from "vitest";
import { publicationsNeedingOverwriteConfirm } from "../bulkGating";
import type { PublicationListItem, PublicationPublishStatus, PublicationSource } from "../types";

function pub(
  id: string,
  source: PublicationSource,
  status: PublicationPublishStatus
): PublicationListItem {
  return {
    id,
    infobaseId: `ib-${id}`,
    infobaseName: id,
    tenantId: "t",
    tenantName: "T",
    siteName: "Default Web Site",
    virtualPath: `/${id}`,
    platformVersion: "8.3.23.1865",
    source,
    lastCheckStatus: status,
    lastCheckAt: null,
    lastCheckDetails: null,
  };
}

describe("publicationsNeedingOverwriteConfirm", () => {
  it("flags non-webinst publications that are already published", () => {
    const gated = publicationsNeedingOverwriteConfirm([
      pub("a", "Configurator", "Published"),
      pub("b", "Unknown", "Published"),
    ]);
    expect(gated.map((p) => p.id)).toEqual(["a", "b"]);
  });

  it("does not flag webinst publications even if published", () => {
    const gated = publicationsNeedingOverwriteConfirm([pub("a", "Webinst", "Published")]);
    expect(gated).toHaveLength(0);
  });

  it("does not flag non-webinst publications that are not published", () => {
    const gated = publicationsNeedingOverwriteConfirm([
      pub("a", "Configurator", "NotPublished"),
      pub("b", "Unknown", "Unknown"),
      pub("c", "Configurator", "Error"),
    ]);
    expect(gated).toHaveLength(0);
  });
});
