import { describe, it, expect, vi, beforeEach } from "vitest";

vi.mock("sonner", () => ({
  toast: { error: vi.fn() },
}));

import { ApiError } from "../api";
import { applyFieldErrors, matchConflictCode, toastFormSubmitError } from "../apiErrors";
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

describe("applyFieldErrors", () => {
  it("400 ValidationProblem: PascalCase-ключ → поле формы (явная карта), первое сообщение", () => {
    const setError = vi.fn();
    const error = new ApiError(400, "bad", {
      errors: { Name: ["Слишком длинно.", "Второе сообщение игнорируется"] },
    });
    expect(applyFieldErrors(error, setError, { Name: "name" })).toBe(true);
    expect(setError).toHaveBeenCalledWith("name", { type: "server", message: "Слишком длинно." });
  });

  it("нормализует ключ без записи в карте: Publication.SiteName → publication.siteName", () => {
    const setError = vi.fn();
    const error = new ApiError(400, "bad", {
      errors: { "Publication.SiteName": ["Укажите сайт."] },
    });
    expect(applyFieldErrors(error, setError)).toBe(true);
    expect(setError).toHaveBeenCalledWith("publication.siteName", {
      type: "server",
      message: "Укажите сайт.",
    });
  });

  it("проставляет несколько полей сразу", () => {
    const setError = vi.fn();
    const error = new ApiError(400, "bad", {
      errors: { Name: ["a"], DatabaseName: ["b"] },
    });
    expect(applyFieldErrors(error, setError)).toBe(true);
    expect(setError).toHaveBeenCalledTimes(2);
    expect(setError).toHaveBeenCalledWith("name", { type: "server", message: "a" });
    expect(setError).toHaveBeenCalledWith("databaseName", { type: "server", message: "b" });
  });

  it("не-400 / без словаря errors / пустой массив → false, setError не зовётся", () => {
    const setError = vi.fn();
    expect(applyFieldErrors(new ApiError(409, "x", { code: "Y" }), setError)).toBe(false);
    expect(applyFieldErrors(new ApiError(400, "x", null), setError)).toBe(false);
    expect(applyFieldErrors(new ApiError(400, "x", { errors: { Name: [] } }), setError)).toBe(
      false
    );
    expect(applyFieldErrors(new Error("boom"), setError)).toBe(false);
    expect(setError).not.toHaveBeenCalled();
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
