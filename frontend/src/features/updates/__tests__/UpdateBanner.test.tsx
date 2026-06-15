import { describe, it, expect, vi, beforeEach } from "vitest";
import { render, screen } from "@testing-library/react";
import "@/i18n";
import { UpdateBanner } from "../UpdateBanner";
import type { UpdateStatus } from "../types";

// Мокаем оба хука: статус обновлений и текущего пользователя (роль управляет
// видимостью кнопки «Скачать»).
vi.mock("../useUpdates", () => ({
  useUpdateStatus: vi.fn(),
}));
vi.mock("@/features/auth/useAuth", () => ({
  useMe: vi.fn(),
}));

import { useUpdateStatus } from "../useUpdates";
import { useMe } from "@/features/auth/useAuth";

const mockedStatus = vi.mocked(useUpdateStatus);
const mockedMe = vi.mocked(useMe);

function setStatus(data: UpdateStatus | undefined) {
  mockedStatus.mockReturnValue({ data } as ReturnType<typeof useUpdateStatus>);
}

function setRoles(roles: string[]) {
  mockedMe.mockReturnValue({
    data: { userName: "u", roles, mustChangePassword: false },
  } as ReturnType<typeof useMe>);
}

const updateReady: UpdateStatus = {
  currentVersion: "0.7.0-beta",
  latestVersion: "0.8.0",
  updateAvailable: true,
  releaseUrl: "https://github.com/sdbsm/MitLicenseCenter/releases/tag/v0.8.0",
  downloadUrl: "https://example/Setup.exe",
  checkAvailable: true,
  checkedAtUtc: "2026-06-16T10:00:00Z",
};

describe("UpdateBanner", () => {
  beforeEach(() => {
    mockedStatus.mockReset();
    mockedMe.mockReset();
    setRoles(["Viewer"]);
  });

  it("скрыт, когда обновления нет (updateAvailable=false)", () => {
    setStatus({ ...updateReady, updateAvailable: false });
    const { container } = render(<UpdateBanner />);
    expect(container).toBeEmptyDOMElement();
  });

  it("скрыт, когда статус ещё не загружен (data undefined)", () => {
    setStatus(undefined);
    const { container } = render(<UpdateBanner />);
    expect(container).toBeEmptyDOMElement();
  });

  it("виден при updateAvailable=true; показывает версию и ссылку на релиз всем", () => {
    setStatus(updateReady);
    render(<UpdateBanner />);
    expect(screen.getByText(/0\.8\.0/)).toBeInTheDocument();
    expect(screen.getByText("Открыть релиз")).toHaveAttribute("href", updateReady.releaseUrl);
  });

  it("кнопка «Скачать» есть у Admin", () => {
    setRoles(["Admin"]);
    setStatus(updateReady);
    render(<UpdateBanner />);
    expect(screen.getByText("Скачать установщик")).toHaveAttribute("href", updateReady.downloadUrl);
  });

  it("кнопка «Скачать» отсутствует у Viewer", () => {
    setRoles(["Viewer"]);
    setStatus(updateReady);
    render(<UpdateBanner />);
    expect(screen.queryByText("Скачать установщик")).not.toBeInTheDocument();
  });

  it("у Admin без downloadUrl кнопки «Скачать» нет", () => {
    setRoles(["Admin"]);
    setStatus({ ...updateReady, downloadUrl: null });
    render(<UpdateBanner />);
    expect(screen.queryByText("Скачать установщик")).not.toBeInTheDocument();
    // Ссылка на релиз при этом остаётся.
    expect(screen.getByText("Открыть релиз")).toBeInTheDocument();
  });
});
