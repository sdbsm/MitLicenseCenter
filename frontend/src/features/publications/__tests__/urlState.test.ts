import { describe, it, expect } from "vitest";
import { parseParams } from "../urlState";

describe("parseParams", () => {
  it("returns empty filters when URLSearchParams is empty", () => {
    const result = parseParams(new URLSearchParams());
    expect(result).toEqual({ tenantId: "", status: "" });
  });

  it("preserves a valid status value", () => {
    const result = parseParams(new URLSearchParams("status=Published"));
    expect(result.status).toBe("Published");
  });

  it("returns empty status for unknown values", () => {
    const result = parseParams(new URLSearchParams("status=XYZ"));
    expect(result.status).toBe("");
  });

  it("preserves an arbitrary tenantId UUID passthrough", () => {
    const uuid = "9b3d1c8e-2f5a-4d6b-8e7c-1a2b3c4d5e6f";
    const result = parseParams(new URLSearchParams(`tenantId=${uuid}`));
    expect(result.tenantId).toBe(uuid);
  });

  it("preserves both filters when both are valid", () => {
    const params = new URLSearchParams("tenantId=tnt-1&status=NotPublished");
    expect(parseParams(params)).toEqual({ tenantId: "tnt-1", status: "NotPublished" });
  });
});
