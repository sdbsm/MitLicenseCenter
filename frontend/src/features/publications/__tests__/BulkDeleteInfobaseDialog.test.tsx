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
import { BulkDeleteInfobaseDialog } from "../BulkDeleteInfobaseDialog";

const mockedApi = vi.mocked(api);

// id публикации намеренно отличается от id инфобазы — ловим, что DELETE идёт по infobaseId,
// а deselect (onRunComplete) — по publicationId.
function pub(publicationId: string, infobaseId: string): PublicationListItem {
  return {
    id: publicationId,
    infobaseId,
    infobaseName: `База ${publicationId}`,
    tenantId: "t1",
    tenantName: "Клиент A",
    siteName: "Default Web Site",
    virtualPath: `/p-${publicationId}`,
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
    <BulkDeleteInfobaseDialog
      open
      onOpenChange={vi.fn()}
      publications={publications}
      onRunComplete={onRunComplete}
    />,
    { wrapper }
  );
  return { onRunComplete, invalidateSpy };
}

describe("BulkDeleteInfobaseDialog (MLC-184b, ADR-45)", () => {
  beforeEach(() => mockedApi.mockReset());

  it("happy-path: DELETE по infobaseId (НЕ publicationId), deselect по publicationId, инвалидация", async () => {
    mockedApi.mockResolvedValue(null);
    const user = userEvent.setup();
    const { onRunComplete, invalidateSpy } = renderDialog([
      pub("pub-1", "ib-1"),
      pub("pub-2", "ib-2"),
    ]);

    await user.click(screen.getByRole("button", { name: /Удалить \(2\)/ }));

    await waitFor(() => expect(onRunComplete).toHaveBeenCalledTimes(1));
    // DELETE — по infobaseId.
    expect(mockedApi).toHaveBeenCalledWith("/api/v1/infobases/ib-1", { method: "DELETE" });
    expect(mockedApi).toHaveBeenCalledWith("/api/v1/infobases/ib-2", { method: "DELETE" });
    expect(invalidateSpy).toHaveBeenCalled();

    // deselect-снимок несёт publicationId (а не infobaseId).
    const states = onRunComplete.mock.calls[0][0];
    expect(states.map((s: { id: string }) => s.id).sort()).toEqual(["pub-1", "pub-2"]);
    expect(states.every((s: { status: string }) => s.status === "ok")).toBe(true);
  });

  it("чекбокс «снять из IIS» (по умолчанию ВЫКЛ) добавляет ?unpublishFromIis=true к DELETE", async () => {
    mockedApi.mockResolvedValue(null);
    const user = userEvent.setup();
    renderDialog([pub("pub-1", "ib-1")]);

    // По умолчанию запрос без query-параметра — проверяем выключенное состояние через клик.
    await user.click(screen.getByLabelText(/Также снять публикации из IIS/));
    await user.click(screen.getByRole("button", { name: /Удалить \(1\)/ }));

    await waitFor(() =>
      expect(mockedApi).toHaveBeenCalledWith("/api/v1/infobases/ib-1?unpublishFromIis=true", {
        method: "DELETE",
      })
    );
  });

  it("без чекбокса DELETE идёт без query-параметра", async () => {
    mockedApi.mockResolvedValue(null);
    const user = userEvent.setup();
    renderDialog([pub("pub-1", "ib-1")]);

    await user.click(screen.getByRole("button", { name: /Удалить \(1\)/ }));

    await waitFor(() =>
      expect(mockedApi).toHaveBeenCalledWith("/api/v1/infobases/ib-1", { method: "DELETE" })
    );
  });

  it("частичный успех: упавший элемент виден ошибкой в прогрессе", async () => {
    mockedApi.mockImplementation((path) => {
      if (String(path).includes("ib-2"))
        return Promise.reject(new ApiError(409, "x", { detail: "конфликт IIS" }));
      return Promise.resolve(null);
    });
    const user = userEvent.setup();
    const { onRunComplete } = renderDialog([pub("pub-1", "ib-1"), pub("pub-2", "ib-2")]);

    await user.click(screen.getByRole("button", { name: /Удалить \(2\)/ }));

    await waitFor(() => expect(onRunComplete).toHaveBeenCalledTimes(1));
    const states = onRunComplete.mock.calls[0][0];
    expect(states.find((s: { id: string }) => s.id === "pub-1")!.status).toBe("ok");
    expect(states.find((s: { id: string }) => s.id === "pub-2")!.status).toBe("error");
    expect(await screen.findByText("конфликт IIS")).toBeInTheDocument();
  });

  it("НЕОБРАТИМО: показывается предупреждение о необратимости", () => {
    renderDialog([pub("pub-1", "ib-1")]);
    expect(screen.getByText(/необратимо/i)).toBeInTheDocument();
  });
});
