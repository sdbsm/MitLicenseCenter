import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import type { ReactNode } from "react";
import { beforeEach, describe, expect, it, vi } from "vitest";
import "@/i18n";
import type * as ApiModule from "@/lib/api";
import type { PublicationListItem } from "../types";

vi.mock("@/lib/api", async (importOriginal) => {
  const actual = await importOriginal<typeof ApiModule>();
  return { ...actual, api: vi.fn() };
});

import { api, ApiError } from "@/lib/api";
import { BulkCheckDialog } from "../BulkCheckDialog";

const mockedApi = vi.mocked(api);

function pub(id: string, infobaseId: string): PublicationListItem {
  return {
    id,
    infobaseId,
    infobaseName: `База ${id}`,
    tenantId: "t1",
    tenantName: "Клиент A",
    siteName: "Default Web Site",
    virtualPath: `/p-${id}`,
    platformVersion: "8.3.23.1865",
    source: "Webinst",
    lastCheckStatus: "Published",
    lastCheckAt: null,
    lastCheckDetails: null,
  };
}

function renderDialog(publications: PublicationListItem[], onRunComplete = vi.fn()) {
  const client = new QueryClient();
  const invalidateSpy = vi.spyOn(client, "invalidateQueries");
  const wrapper = ({ children }: { children: ReactNode }) => (
    <QueryClientProvider client={client}>{children}</QueryClientProvider>
  );
  render(
    <BulkCheckDialog
      open
      onOpenChange={vi.fn()}
      publications={publications}
      onRunComplete={onRunComplete}
    />,
    { wrapper }
  );
  return { onRunComplete, invalidateSpy };
}

describe("BulkCheckDialog (MLC-184b)", () => {
  beforeEach(() => mockedApi.mockReset());

  it("happy-path: бьёт /check по каждому id, инвалидирует список и зовёт onRunComplete", async () => {
    mockedApi.mockResolvedValue({ status: "Published" });
    const user = userEvent.setup();
    const { onRunComplete, invalidateSpy } = renderDialog([pub("p1", "ib1"), pub("p2", "ib2")]);

    await user.click(screen.getByRole("button", { name: /Проверить \(2\)/ }));

    await waitFor(() => expect(onRunComplete).toHaveBeenCalledTimes(1));
    expect(mockedApi).toHaveBeenCalledWith("/api/v1/publications/p1/check", { method: "POST" });
    expect(mockedApi).toHaveBeenCalledWith("/api/v1/publications/p2/check", { method: "POST" });
    expect(invalidateSpy).toHaveBeenCalled();

    const states = onRunComplete.mock.calls[0][0];
    expect(states.every((s: { status: string }) => s.status === "ok")).toBe(true);
  });

  it("частичный успех: упавший элемент виден ошибкой в прогрессе", async () => {
    mockedApi.mockImplementation((path) => {
      if (String(path).includes("/p2/"))
        return Promise.reject(new ApiError(409, "x", { detail: "занят" }));
      return Promise.resolve({ status: "Published" });
    });
    const user = userEvent.setup();
    const { onRunComplete } = renderDialog([pub("p1", "ib1"), pub("p2", "ib2")]);

    await user.click(screen.getByRole("button", { name: /Проверить \(2\)/ }));

    await waitFor(() => expect(onRunComplete).toHaveBeenCalledTimes(1));
    const states = onRunComplete.mock.calls[0][0];
    expect(states.find((s: { id: string }) => s.id === "p1")!.status).toBe("ok");
    expect(states.find((s: { id: string }) => s.id === "p2")!.status).toBe("error");
    expect(await screen.findByText("занят")).toBeInTheDocument();
  });

  it("read-only: нет предупреждения о необратимости", () => {
    renderDialog([pub("p1", "ib1")]);
    expect(screen.queryByText(/необратимо/i)).not.toBeInTheDocument();
  });
});
