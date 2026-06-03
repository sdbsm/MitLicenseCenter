import { describe, it, expect, vi, beforeEach } from "vitest";
import { render, screen, waitFor } from "@testing-library/react";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import type { ReactNode } from "react";
import "@/i18n";
import type * as ApiModule from "@/lib/api";
import { SettingsPage } from "../SettingsPage";
import type { SettingDescriptor } from "../types";

// Частичный мок: подменяем только сетевой `api`, остальное (ApiError) — настоящее.
vi.mock("@/lib/api", async (importOriginal) => {
  const actual = await importOriginal<typeof ApiModule>();
  return { ...actual, api: vi.fn() };
});

import { api } from "@/lib/api";

const mockedApi = vi.mocked(api);

function descriptor(key: string, value: string | null): SettingDescriptor {
  return {
    key,
    isSecret: false,
    isSet: value !== null,
    value,
    description: null,
    updatedAt: "2026-01-01T00:00:00Z",
    updatedBy: "System",
  };
}

function setup() {
  const client = new QueryClient({
    defaultOptions: { queries: { retry: false }, mutations: { retry: false } },
  });
  const wrapper = ({ children }: { children: ReactNode }) => (
    <QueryClientProvider client={client}>{children}</QueryClientProvider>
  );
  render(<SettingsPage />, { wrapper });
}

describe("SettingsPage — OneC.LicenseConsumingAppIds (MLC-024)", () => {
  beforeEach(() => {
    mockedApi.mockReset();
  });

  it("рендерит поле whitelist'а лицензионных app-id из каталога настроек", async () => {
    mockedApi.mockResolvedValue([
      descriptor("OneC.LicenseConsumingAppIds", "1CV8,1CV8C,WebClient,Designer,COMConnection"),
    ]);

    setup();

    // Label из i18n + текстовый input с засеянным значением — поле появилось на странице.
    expect(await screen.findByText("App-id, потребляющие лицензию")).toBeInTheDocument();
    const input = await waitFor(() => screen.getByLabelText("App-id, потребляющие лицензию"));
    expect(input).toHaveValue("1CV8,1CV8C,WebClient,Designer,COMConnection");
  });
});
