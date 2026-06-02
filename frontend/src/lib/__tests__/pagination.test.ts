import { describe, expect, it } from "vitest";
import { pageLinkRange } from "../pagination";

describe("pageLinkRange", () => {
  it("returns every page when they fit within maxLinks", () => {
    expect(pageLinkRange(1, 1)).toEqual([1]);
    expect(pageLinkRange(3, 5)).toEqual([1, 2, 3, 4, 5]);
    expect(pageLinkRange(7, 7)).toEqual([1, 2, 3, 4, 5, 6, 7]);
  });

  it("keeps a sliding window of maxLinks pages when there are more", () => {
    expect(pageLinkRange(1, 20)).toEqual([1, 2, 3, 4, 5, 6, 7]);
    expect(pageLinkRange(10, 20)).toEqual([7, 8, 9, 10, 11, 12, 13]);
    expect(pageLinkRange(20, 20)).toEqual([14, 15, 16, 17, 18, 19, 20]);
  });

  it("clamps the window to the edges", () => {
    expect(pageLinkRange(2, 20)).toEqual([1, 2, 3, 4, 5, 6, 7]);
    expect(pageLinkRange(19, 20)).toEqual([14, 15, 16, 17, 18, 19, 20]);
  });

  it("honours a custom maxLinks", () => {
    expect(pageLinkRange(5, 20, 3)).toEqual([4, 5, 6]);
  });
});
