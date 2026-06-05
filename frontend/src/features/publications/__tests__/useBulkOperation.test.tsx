import { act, renderHook, waitFor } from "@testing-library/react";
import { describe, expect, it, vi } from "vitest";
import { ApiError } from "@/lib/api";
import { useBulkOperation, type BulkItemState } from "../useBulkOperation";

function items(...ids: string[]) {
  return ids.map((id) => ({ id, label: id }));
}

describe("useBulkOperation", () => {
  it("runs every item and reports all successful", async () => {
    const runItem = vi.fn(async () => {});
    const { result } = renderHook(() => useBulkOperation({ runItem, describeError: () => "e" }));

    await act(async () => {
      await result.current.start(items("a", "b", "c"));
    });

    expect(runItem).toHaveBeenCalledTimes(3);
    expect(result.current.phase).toBe("done");
    expect(result.current.summary).toMatchObject({ total: 3, ok: 3, error: 0, skipped: 0 });
  });

  it("collects partial success: failed item carries a described error", async () => {
    const runItem = vi.fn(async (id: string) => {
      if (id === "b") throw new ApiError(409, "x", { detail: "nope" });
    });
    const describeError = (e: unknown) => (e instanceof ApiError ? "conflict" : "other");

    const { result } = renderHook(() => useBulkOperation({ runItem, describeError }));

    await act(async () => {
      await result.current.start(items("a", "b", "c"));
    });

    expect(result.current.summary).toMatchObject({ ok: 2, error: 1 });
    const b = result.current.states.find((s) => s.id === "b")!;
    expect(b.status).toBe("error");
    expect(b.error).toBe("conflict");
  });

  it("never runs more than `concurrency` operations at once", async () => {
    let active = 0;
    let max = 0;
    const runItem = vi.fn(async () => {
      active += 1;
      max = Math.max(max, active);
      await new Promise((r) => setTimeout(r, 10));
      active -= 1;
    });

    const { result } = renderHook(() =>
      useBulkOperation({ concurrency: 3, runItem, describeError: () => "e" })
    );

    await act(async () => {
      await result.current.start(items("a", "b", "c", "d", "e", "f", "g", "h", "i"));
    });

    expect(max).toBe(3);
    expect(result.current.summary.ok).toBe(9);
  });

  it("cancel stops scheduling new items; remaining are skipped", async () => {
    const resolvers: Record<string, () => void> = {};
    const runItem = (id: string) => new Promise<void>((res) => (resolvers[id] = res));

    const { result } = renderHook(() =>
      useBulkOperation({ concurrency: 2, runItem, describeError: () => "e" })
    );

    let startPromise!: Promise<void>;
    act(() => {
      startPromise = result.current.start(items("a", "b", "c", "d"));
    });

    // Первые два (по лимиту) запущены, c/d ещё ждут.
    await waitFor(() => expect(Object.keys(resolvers).sort()).toEqual(["a", "b"]));

    act(() => result.current.cancel());
    act(() => {
      resolvers["a"]();
      resolvers["b"]();
    });

    await act(async () => {
      await startPromise;
    });

    expect(Object.keys(resolvers).sort()).toEqual(["a", "b"]); // c/d не запускались
    expect(result.current.summary).toMatchObject({ ok: 2, skipped: 2 });
    const c = result.current.states.find((s) => s.id === "c")!;
    expect(c.status).toBe("skipped");
  });

  it("passes the final snapshot to onComplete (ok ids for deselect)", async () => {
    const onComplete = vi.fn<(states: BulkItemState[]) => void>();
    const runItem = vi.fn(async (id: string) => {
      if (id === "b") throw new Error("x");
    });

    const { result } = renderHook(() =>
      useBulkOperation({ runItem, describeError: () => "e", onComplete })
    );

    await act(async () => {
      await result.current.start(items("a", "b"));
    });

    expect(onComplete).toHaveBeenCalledTimes(1);
    const states = onComplete.mock.calls[0][0];
    expect(states.find((s) => s.id === "a")!.status).toBe("ok");
    expect(states.find((s) => s.id === "b")!.status).toBe("error");
  });
});
