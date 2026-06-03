import { describe, it, expect, vi, beforeEach } from "vitest";

vi.mock("sonner", () => ({
  toast: { error: vi.fn() },
}));

import { ApiError } from "../api";
import { matchConflictCode, toastFormSubmitError } from "../apiErrors";
import { toast } from "sonner";

const mockedToastError = vi.mocked(toast.error);

// MLC-033 — обобщённый 409-классификатор и общий хвост submit-ошибки форм, поднятые из
// заинлайненной логики диалогов (mapConflictToField, MLC-023). Проверяем классификацию
// и fallback напрямую — без рендера; контракт ConflictBody/readConflictBody не трогается.

describe("matchConflictCode", () => {
  const table = { NAME_DUPLICATE: { field: "name" } } as const;

  it("409 с совпавшим code → дескриптор из таблицы", () => {
    expect(
      matchConflictCode(new ApiError(409, "conflict", { code: "NAME_DUPLICATE" }), table)
    ).toEqual({ field: "name" });
  });

  it("409 с неизвестным code → null", () => {
    expect(
      matchConflictCode(new ApiError(409, "conflict", { code: "SOMETHING_ELSE" }), table)
    ).toBeNull();
  });

  it("409 без тела → null", () => {
    expect(matchConflictCode(new ApiError(409, "conflict", null), table)).toBeNull();
  });

  it("409 с телом без code → null", () => {
    expect(matchConflictCode(new ApiError(409, "conflict", { detail: "x" }), table)).toBeNull();
  });

  it("не-409 статусы → null", () => {
    expect(
      matchConflictCode(new ApiError(400, "bad", { code: "NAME_DUPLICATE" }), table)
    ).toBeNull();
    expect(
      matchConflictCode(new ApiError(404, "nf", { code: "NAME_DUPLICATE" }), table)
    ).toBeNull();
  });

  it("не-ApiError → null", () => {
    expect(matchConflictCode(new Error("boom"), table)).toBeNull();
    expect(matchConflictCode(undefined, table)).toBeNull();
  });
});

describe("toastFormSubmitError", () => {
  const t = (key: string) => (key === "errors.generic" ? "Что-то пошло не так." : key);

  beforeEach(() => {
    mockedToastError.mockReset();
  });

  it("400 с message → серверное сообщение", () => {
    toastFormSubmitError(new ApiError(400, "Поле обязательно.", null), t);
    expect(mockedToastError).toHaveBeenCalledWith("Поле обязательно.");
  });

  it("400 с пустым message → generic", () => {
    toastFormSubmitError(new ApiError(400, "", null), t);
    expect(mockedToastError).toHaveBeenCalledWith("Что-то пошло не так.");
  });

  it("прочие ошибки → generic", () => {
    toastFormSubmitError(new ApiError(409, "conflict", null), t);
    expect(mockedToastError).toHaveBeenCalledWith("Что-то пошло не так.");

    toastFormSubmitError(new Error("boom"), t);
    expect(mockedToastError).toHaveBeenCalledWith("Что-то пошло не так.");
  });
});
