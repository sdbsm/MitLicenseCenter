import { describe, it, expect } from "vitest";
import { virtualPathFromDatabase, physicalPathFromDatabase } from "../paths";

describe("virtualPathFromDatabase", () => {
  it("slugifies a typical latin database name", () => {
    expect(virtualPathFromDatabase("acme_bp")).toBe("/acme-bp");
  });

  it("lowercases and collapses non-alphanumeric runs to a single dash", () => {
    expect(virtualPathFromDatabase("ACME  BP__2024")).toBe("/acme-bp-2024");
  });

  it("trims leading and trailing separators", () => {
    expect(virtualPathFromDatabase("_acme_")).toBe("/acme");
  });

  it("returns empty string when nothing slugifiable remains (e.g. cyrillic)", () => {
    // Cyrillic has no a-z0-9 chars → empty slug → empty path (operator fills it manually).
    expect(virtualPathFromDatabase("Бухгалтерия")).toBe("");
    expect(virtualPathFromDatabase("")).toBe("");
  });
});

describe("physicalPathFromDatabase", () => {
  it("joins root and the raw database name (no site segment)", () => {
    expect(physicalPathFromDatabase("C:\\inetpub\\wwwroot", "acme_bp")).toBe(
      "C:\\inetpub\\wwwroot\\acme_bp"
    );
  });

  it("trims a trailing slash/backslash from the root", () => {
    expect(physicalPathFromDatabase("C:\\inetpub\\wwwroot\\", "acme_bp")).toBe(
      "C:\\inetpub\\wwwroot\\acme_bp"
    );
  });

  it("keeps the database name verbatim (not slugified)", () => {
    expect(physicalPathFromDatabase("D:\\pub", "Acme_BP")).toBe("D:\\pub\\Acme_BP");
  });

  it("returns empty string when the database name is blank", () => {
    expect(physicalPathFromDatabase("C:\\inetpub\\wwwroot", "")).toBe("");
  });
});
