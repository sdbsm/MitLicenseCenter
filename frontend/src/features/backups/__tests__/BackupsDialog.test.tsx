import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import type { ReactNode } from "react";
import { beforeEach, describe, expect, it, vi } from "vitest";
import "@/i18n";
import type * as ApiModule from "@/lib/api";
import type { InfobaseListItem } from "@/features/infobases/types";
import { BackupsDialog } from "../BackupsDialog";
import type { BackupSummary } from "../types";

vi.mock("sonner", () => ({
  toast: { success: vi.fn(), error: vi.fn() },
}));

vi.mock("@/lib/api", async (importOriginal) => {
  const actual = await importOriginal<typeof ApiModule>();
  return { ...actual, api: vi.fn() };
});

vi.mock("@/features/auth/useAuth", () => ({
  useMe: vi.fn(),
}));

import { api, ApiError } from "@/lib/api";
import { useMe } from "@/features/auth/useAuth";
import { toast } from "sonner";

const mockedApi = vi.mocked(api);
const mockedUseMe = vi.mocked(useMe);
const mockedToastError = vi.mocked(toast.error);

const infobase = {
  id: "22222222-2222-2222-2222-222222222222",
  tenantId: "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa",
  name: "Бухгалтерия",
  clusterInfobaseId: "dddddddd-dddd-dddd-dddd-dddddddddddd",
  databaseName: "acme_bp",
  status: "Active",
  createdAt: "2026-01-01T00:00:00Z",
  updatedAt: null,
  tenantName: "Клиент A",
  publication: {
    id: "eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee",
    infobaseId: "22222222-2222-2222-2222-222222222222",
    siteName: "Default Web Site",
    virtualPath: "/acme-bp",
    platformVersion: "8.3.23.1865",
    source: "Unknown",
    physicalPathOverride: null,
    createdAt: "2026-01-01T00:00:00Z",
    updatedAt: null,
    lastCheckStatus: "Unknown",
    lastCheckAt: null,
    lastCheckDetails: null,
  },
} as InfobaseListItem;

const succeeded: BackupSummary = {
  id: "11111111-1111-1111-1111-111111111111",
  infobaseId: infobase.id,
  databaseServer: "(local)",
  databaseName: "acme_bp",
  status: "Succeeded",
  requestedBy: "operator",
  requestedAtUtc: "2026-06-10T08:00:00Z",
  startedAtUtc: "2026-06-10T08:00:05Z",
  completedAtUtc: "2026-06-10T08:01:10Z",
  filePath: "D:\\Backups\\acme_bp\\acme_bp_20260610_080005.bak",
  fileSizeBytes: 123_456_789,
  failureReason: "None",
  errorMessage: null,
};

const queued: BackupSummary = {
  ...succeeded,
  id: "33333333-3333-3333-3333-333333333333",
  status: "Queued",
  startedAtUtc: null,
  completedAtUtc: null,
  filePath: null,
  fileSizeBytes: null,
};

const failed: BackupSummary = {
  ...queued,
  id: "44444444-4444-4444-4444-444444444444",
  status: "Failed",
  failureReason: "InsufficientSpace",
  errorMessage: "Свободно 1024 МБ, требуется 4096 МБ.",
};

function setMe(roles: string[]) {
  mockedUseMe.mockReturnValue({
    data: { userName: "test", roles },
  } as unknown as ReturnType<typeof useMe>);
}

function setup(backups: BackupSummary[] | Promise<BackupSummary[]> = []) {
  // Эндпоинт пагинирован (MLC-130): GET отдаёт конверт { items, total, page, pageSize }.
  const toPaged = (items: BackupSummary[]) => ({
    items,
    total: items.length,
    page: 1,
    pageSize: 100,
  });
  mockedApi.mockImplementation((path: string) =>
    path.startsWith("/api/v1/backups?")
      ? Promise.resolve(backups).then(toPaged)
      : Promise.reject(new Error(path))
  );
  const client = new QueryClient({
    defaultOptions: { queries: { retry: false }, mutations: { retry: false } },
  });
  const wrapper = ({ children }: { children: ReactNode }) => (
    <QueryClientProvider client={client}>{children}</QueryClientProvider>
  );
  render(<BackupsDialog open onOpenChange={vi.fn()} infobase={infobase} />, { wrapper });
  return { user: userEvent.setup() };
}

describe("BackupsDialog", () => {
  beforeEach(() => {
    mockedApi.mockReset();
    mockedToastError.mockReset();
    setMe(["Admin"]);
  });

  it("рендерит статусы и значения: Готов с размером, В очереди с «—», Ошибка с причиной по-русски", async () => {
    setup([succeeded, queued, failed]);

    expect(await screen.findByText("Готов")).toBeInTheDocument();
    expect(screen.getByText("117.7 МБ")).toBeInTheDocument();
    expect(screen.getByText("В очереди")).toBeInTheDocument();
    expect(screen.getByText("Ошибка")).toBeInTheDocument();
    expect(screen.getByText("Недостаточно свободного места на диске")).toBeInTheDocument();
    // Queued: завершение и размер ещё неизвестны → честное «—», не нули.
    expect(screen.getAllByText("—").length).toBeGreaterThan(0);
  });

  it("Admin видит кнопки удаления", async () => {
    setup([succeeded]);
    expect(await screen.findByText("Готов")).toBeInTheDocument();
    expect(screen.getAllByRole("button", { name: "Удалить бэкап" })).toHaveLength(1);
  });

  it("Viewer: кнопка «Сделать бэкап» есть, удаления нет", async () => {
    setMe(["Viewer"]);
    setup([succeeded]);

    expect(await screen.findByText("Готов")).toBeInTheDocument();
    expect(screen.getByRole("button", { name: /Сделать бэкап/ })).toBeInTheDocument();
    expect(screen.queryByRole("button", { name: "Удалить бэкап" })).not.toBeInTheDocument();
  });

  it("пустое состояние «Бэкапов ещё нет»", async () => {
    setup([]);
    expect(await screen.findByText("Бэкапов ещё нет")).toBeInTheDocument();
  });

  it("409 BACKUP_ACTIVE на старте → понятный тост", async () => {
    const { user } = setup([queued]);
    expect(await screen.findByText("В очереди")).toBeInTheDocument();

    mockedApi.mockRejectedValueOnce(new ApiError(409, "conflict", { code: "BACKUP_ACTIVE" }));
    await user.click(screen.getByRole("button", { name: /Сделать бэкап/ }));

    await waitFor(() =>
      expect(mockedToastError).toHaveBeenCalledWith(
        "Бэкап этой базы уже выполняется или стоит в очереди."
      )
    );
  });

  it("409 BACKUP_FOLDER_NOT_CONFIGURED → тост с подсказкой про «Параметры»", async () => {
    const { user } = setup([]);
    expect(await screen.findByText("Бэкапов ещё нет")).toBeInTheDocument();

    mockedApi.mockRejectedValueOnce(
      new ApiError(409, "conflict", { code: "BACKUP_FOLDER_NOT_CONFIGURED" })
    );
    await user.click(screen.getByRole("button", { name: /Сделать бэкап/ }));

    await waitFor(() =>
      expect(mockedToastError).toHaveBeenCalledWith(
        "Папка бэкапов не настроена. Задайте «Папка для бэкапов» в разделе «Параметры»."
      )
    );
  });
});
