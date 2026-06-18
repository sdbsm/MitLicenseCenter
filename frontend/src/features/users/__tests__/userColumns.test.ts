import { describe, it, expect } from "vitest";
import { EyeIcon, ShieldCheckIcon } from "lucide-react";
import { initialsOf, roleIconFor } from "../userColumns";

describe("initialsOf", () => {
  it("два слова через пробел → первые буквы двух слов", () => {
    expect(initialsOf("Ivan Petrov")).toBe("IP");
  });

  it("два слова через точку → первые буквы двух слов", () => {
    expect(initialsOf("ivan.petrov")).toBe("IP");
  });

  it("три слова → первые буквы первых двух", () => {
    expect(initialsOf("Иван Иванович Петров")).toBe("ИИ");
  });

  it("одно слово длиннее 2 символов → первые два символа", () => {
    expect(initialsOf("admin")).toBe("AD");
  });

  it("одно слово из одного символа → один символ", () => {
    expect(initialsOf("a")).toBe("A");
  });

  it("пустая строка → пустая строка", () => {
    expect(initialsOf("")).toBe("");
  });

  it("строка из пробелов (только разделители) → пустая строка", () => {
    expect(initialsOf("   ")).toBe("");
  });

  it("кириллица работает корректно", () => {
    expect(initialsOf("Петров Иван")).toBe("ПИ");
  });

  it("только один символ слова после разделения", () => {
    expect(initialsOf("a.b")).toBe("AB");
  });
});

describe("roleIconFor", () => {
  it("Admin → ShieldCheckIcon", () => {
    expect(roleIconFor(["Admin"])).toBe(ShieldCheckIcon);
  });

  it("Viewer → EyeIcon", () => {
    expect(roleIconFor(["Viewer"])).toBe(EyeIcon);
  });

  it("Admin + Viewer → Admin имеет приоритет → ShieldCheckIcon", () => {
    expect(roleIconFor(["Admin", "Viewer"])).toBe(ShieldCheckIcon);
  });

  it("неизвестная роль → null", () => {
    expect(roleIconFor(["Unknown"])).toBeNull();
  });

  it("пустой массив → null", () => {
    expect(roleIconFor([])).toBeNull();
  });
});
