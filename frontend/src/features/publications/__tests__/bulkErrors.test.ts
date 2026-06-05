import type { TFunction } from "i18next";
import { describe, expect, it } from "vitest";
import { ApiError } from "@/lib/api";
import { describePublicationOpError } from "../bulkErrors";

// Стаб t: возвращает ключ — проверяем, какую ветку выбрал маппер.
const t = ((key: string) => key) as unknown as TFunction;

describe("describePublicationOpError", () => {
  it("uses server detail for 409 (gate / webinst / IIS failure)", () => {
    const msg = describePublicationOpError(new ApiError(409, "x", { detail: "webinst упал" }), t);
    expect(msg).toBe("webinst упал");
  });

  it("falls back to a generic conflict text for 409 without detail", () => {
    const msg = describePublicationOpError(new ApiError(409, "x", null), t);
    expect(msg).toBe("publications.bulk.errors.conflict");
  });

  it("maps 404 to not-found", () => {
    expect(describePublicationOpError(new ApiError(404, "x", null), t)).toBe(
      "publications.bulk.errors.notFound"
    );
  });

  it("uses server detail for 422 validation, else generic validation text", () => {
    expect(
      describePublicationOpError(new ApiError(422, "x", { detail: "версия не установлена" }), t)
    ).toBe("версия не установлена");
    expect(describePublicationOpError(new ApiError(400, "x", null), t)).toBe(
      "publications.bulk.errors.validation"
    );
  });

  it("falls back to generic error for non-ApiError", () => {
    expect(describePublicationOpError(new Error("boom"), t)).toBe("errors.generic");
  });
});
