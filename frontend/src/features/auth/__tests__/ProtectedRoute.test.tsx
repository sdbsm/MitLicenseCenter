import { describe, it, expect, vi, beforeEach } from "vitest";
import { render, screen } from "@testing-library/react";
import { MemoryRouter, Route, Routes } from "react-router";
import { ProtectedRoute } from "../ProtectedRoute";
import type { CurrentUser } from "../useAuth";

vi.mock("../useAuth", () => ({
  useMe: vi.fn(),
}));

// Экран форс-смены — отдельная единица; здесь проверяем только маршрутный гейт, поэтому
// подменяем его маркером (его собственное поведение покрыто отдельно).
vi.mock("../ForcePasswordChange", () => ({
  ForcePasswordChange: () => <div>FORCE CHANGE SCREEN</div>,
}));

import { useMe } from "../useAuth";

const mockedUseMe = vi.mocked(useMe);

type MeState = {
  data?: CurrentUser | null;
  isLoading?: boolean;
  isError?: boolean;
};

function setMe(state: MeState) {
  mockedUseMe.mockReturnValue({
    data: state.data ?? null,
    isLoading: state.isLoading ?? false,
    isError: state.isError ?? false,
  } as ReturnType<typeof useMe>);
}

// Рендерит дерево с маршрутами-маркерами для "/" и "/login", чтобы наблюдать,
// куда ProtectedRoute редиректит. `requireAdmin` управляет admin-only веткой.
function renderProtected(requireAdmin: boolean) {
  return render(
    <MemoryRouter initialEntries={["/protected"]}>
      <Routes>
        <Route path="/login" element={<div>LOGIN PAGE</div>} />
        <Route path="/" element={<div>HOME PAGE</div>} />
        <Route
          path="/protected"
          element={
            <ProtectedRoute requireAdmin={requireAdmin}>
              <div>PROTECTED CONTENT</div>
            </ProtectedRoute>
          }
        />
      </Routes>
    </MemoryRouter>
  );
}

describe("ProtectedRoute", () => {
  beforeEach(() => {
    mockedUseMe.mockReset();
  });

  it("пускает авторизованного пользователя к содержимому", () => {
    setMe({ data: { userName: "viewer", roles: ["Viewer"], mustChangePassword: false } });
    renderProtected(false);
    expect(screen.getByText("PROTECTED CONTENT")).toBeInTheDocument();
    expect(screen.queryByText("LOGIN PAGE")).not.toBeInTheDocument();
  });

  it("редиректит неавторизованного на /login", () => {
    setMe({ data: null, isError: true });
    renderProtected(false);
    expect(screen.getByText("LOGIN PAGE")).toBeInTheDocument();
    expect(screen.queryByText("PROTECTED CONTENT")).not.toBeInTheDocument();
  });

  it("редиректит на /login, когда данных о пользователе нет (без ошибки)", () => {
    setMe({ data: null, isError: false });
    renderProtected(false);
    expect(screen.getByText("LOGIN PAGE")).toBeInTheDocument();
  });

  it("admin-only маршрут редиректит Viewer на /", () => {
    setMe({ data: { userName: "viewer", roles: ["Viewer"], mustChangePassword: false } });
    renderProtected(true);
    expect(screen.getByText("HOME PAGE")).toBeInTheDocument();
    expect(screen.queryByText("PROTECTED CONTENT")).not.toBeInTheDocument();
    expect(screen.queryByText("LOGIN PAGE")).not.toBeInTheDocument();
  });

  it("admin-only маршрут пускает Admin к содержимому", () => {
    setMe({ data: { userName: "admin", roles: ["Admin"], mustChangePassword: false } });
    renderProtected(true);
    expect(screen.getByText("PROTECTED CONTENT")).toBeInTheDocument();
  });

  it("показывает экран форс-смены, пока стоит mustChangePassword", () => {
    setMe({ data: { userName: "operator", roles: ["Admin"], mustChangePassword: true } });
    renderProtected(false);
    expect(screen.getByText("FORCE CHANGE SCREEN")).toBeInTheDocument();
    expect(screen.queryByText("PROTECTED CONTENT")).not.toBeInTheDocument();
    expect(screen.queryByText("LOGIN PAGE")).not.toBeInTheDocument();
  });

  it("во время загрузки не редиректит и не показывает содержимое", () => {
    setMe({ data: null, isLoading: true });
    renderProtected(false);
    expect(screen.queryByText("PROTECTED CONTENT")).not.toBeInTheDocument();
    expect(screen.queryByText("LOGIN PAGE")).not.toBeInTheDocument();
    expect(screen.queryByText("HOME PAGE")).not.toBeInTheDocument();
  });
});
