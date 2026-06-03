import { describe, it, expect } from "vitest";
import { ApiError } from "@/lib/api";
import { mapConflictToField } from "../mapConflictToField";

// MLC-023 — чистый маппинг ошибки API → дескриптор ошибки поля. Парный смыслу того, что
// раньше было заинлайнено в onSubmit InfobaseFormDialog. i18n/setError/toast здесь не
// участвуют (их делает useInfobaseForm) — проверяем только классификацию.

describe("mapConflictToField", () => {
  it("409 NAME_DUPLICATE_IN_TENANT → поле name + раскрытие «Дополнительно»", () => {
    const result = mapConflictToField(
      new ApiError(409, "conflict", { code: "NAME_DUPLICATE_IN_TENANT" })
    );
    expect(result).toEqual({
      field: "name",
      messageKey: "infobases.errors.nameDuplicate",
      openAdvanced: true,
    });
  });

  it("409 INFOBASE_ALREADY_ASSIGNED → поле clusterInfobaseId без раскрытия", () => {
    const result = mapConflictToField(
      new ApiError(409, "conflict", { code: "INFOBASE_ALREADY_ASSIGNED" })
    );
    expect(result).toEqual({
      field: "clusterInfobaseId",
      messageKey: "infobases.errors.clusterAlreadyAssigned",
    });
  });

  it("404 → поле tenantId", () => {
    const result = mapConflictToField(new ApiError(404, "not found", null));
    expect(result).toEqual({ field: "tenantId", messageKey: "infobases.errors.tenantNotFound" });
  });

  it("409 с неизвестным кодом → null (fallback на toast у вызывающей стороны)", () => {
    expect(
      mapConflictToField(new ApiError(409, "conflict", { code: "SOMETHING_ELSE" }))
    ).toBeNull();
  });

  it("409 без тела → null", () => {
    expect(mapConflictToField(new ApiError(409, "conflict", null))).toBeNull();
  });

  it("400 → null (вызывающая сторона показывает message-toast)", () => {
    expect(mapConflictToField(new ApiError(400, "bad request", null))).toBeNull();
  });

  it("не-ApiError → null", () => {
    expect(mapConflictToField(new Error("boom"))).toBeNull();
    expect(mapConflictToField(undefined)).toBeNull();
  });
});
