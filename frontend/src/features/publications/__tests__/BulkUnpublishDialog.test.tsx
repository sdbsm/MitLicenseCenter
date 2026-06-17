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
import { BulkUnpublishDialog } from "../BulkUnpublishDialog";

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
    <BulkUnpublishDialog
      open
      onOpenChange={vi.fn()}
      publications={publications}
      onRunComplete={onRunComplete}
    />,
    { wrapper }
  );
  return { onRunComplete, invalidateSpy };
}

describe("BulkUnpublishDialog (MLC-184b, ADR-45)", () => {
  beforeEach(() => mockedApi.mockReset());

  it("happy-path: бьёт /unpublish по каждому id, инвалидирует список и зовёт onRunComplete", async () => {
    mockedApi.mockResolvedValue({ status: "NotPublished" });
    const user = userEvent.setup();
    const { onRunComplete, invalidateSpy } = renderDialog([pub("p1", "ib1"), pub("p2", "ib2")]);

    await user.click(screen.getByRole("button", { name: /Снять с публикации \(2\)/ }));

    await waitFor(() => expect(onRunComplete).toHaveBeenCalledTimes(1));
    expect(mockedApi).toHaveBeenCalledWith("/api/v1/publications/p1/unpublish", { method: "POST" });
    expect(mockedApi).toHaveBeenCalledWith("/api/v1/publications/p2/unpublish", { method: "POST" });
    expect(invalidateSpy).toHaveBeenCalled();
  });

  it("частичный успех: упавший элемент виден ошибкой в прогрессе", async () => {
    mockedApi.mockImplementation((path) => {
      if (String(path).includes("/p2/"))
        return Promise.reject(new ApiError(409, "x", { detail: "блокирован" }));
      return Promise.resolve({ status: "NotPublished" });
    });
    const user = userEvent.setup();
    const { onRunComplete } = renderDialog([pub("p1", "ib1"), pub("p2", "ib2")]);

    await user.click(screen.getByRole("button", { name: /Снять с публикации \(2\)/ }));

    await waitFor(() => expect(onRunComplete).toHaveBeenCalledTimes(1));
    const states = onRunComplete.mock.calls[0][0];
    expect(states.find((s: { id: string }) => s.id === "p1")!.status).toBe("ok");
    expect(states.find((s: { id: string }) => s.id === "p2")!.status).toBe("error");
    expect(await screen.findByText("блокирован")).toBeInTheDocument();
  });

  it("обратимое действие: НЕТ предупреждения о необратимости, кнопка destructive-стиля", () => {
    renderDialog([pub("p1", "ib1")]);
    expect(screen.queryByText(/необратимо/i)).not.toBeInTheDocument();
    expect(screen.getByRole("button", { name: /Снять с публикации \(1\)/ })).toHaveAttribute(
      "data-variant",
      "destructive"
    );
  });
});
