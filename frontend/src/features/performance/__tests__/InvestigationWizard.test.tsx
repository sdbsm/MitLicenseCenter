import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { describe, expect, it, vi, beforeEach } from "vitest";
import "@/i18n";

/**
 * Тесты Мастера запуска расследования — экран 2 (MLC-242/248).
 *
 * Фокус MLC-248: поле «Порог длительности запроса, c» показывается ТОЛЬКО для сценариев
 * SlowQueries/GeneralSlow, шлётся как slowQueryThresholdSeconds; для прочих сценариев — скрыто и не шлётся.
 */

const mutateAsync = vi.fn().mockResolvedValue(undefined);

vi.mock("@/features/auth/useAuth", () => ({
  useMe: () => ({ data: { roles: ["Admin"] } }),
}));

vi.mock("@/features/infobases/useInfobases", () => ({
  useInfobases: () => ({
    data: { items: [{ id: "ib-1", name: "demodb", tenantName: "ООО «МитПро»" }] },
  }),
}));

vi.mock("@/features/investigations/useInvestigations", () => ({
  useStartInvestigation: () => ({ mutateAsync, isPending: false }),
}));

import { InvestigationWizard } from "../InvestigationWizard";

async function selectScenario(user: ReturnType<typeof userEvent.setup>, label: RegExp) {
  await user.click(screen.getByRole("combobox"));
  await user.click(await screen.findByRole("option", { name: label }));
}

describe("InvestigationWizard — поле порога (MLC-248)", () => {
  beforeEach(() => {
    mutateAsync.mockClear();
  });

  it("поле порога скрыто, пока сценарий не выбран", () => {
    render(<InvestigationWizard />);
    expect(screen.queryByLabelText(/Порог длительности запроса/i)).not.toBeInTheDocument();
  });

  it("поле порога показывается для сценария «Долгие запросы»", async () => {
    const user = userEvent.setup();
    render(<InvestigationWizard />);
    await selectScenario(user, /Долгие запросы/i);
    expect(screen.getByLabelText(/Порог длительности запроса/i)).toBeInTheDocument();
  });

  it("поле порога скрыто для сценария «Блокировки»", async () => {
    const user = userEvent.setup();
    render(<InvestigationWizard />);
    await selectScenario(user, /^Управляемые блокировки/i);
    expect(screen.queryByLabelText(/Порог длительности запроса/i)).not.toBeInTheDocument();
  });

  it("шлёт slowQueryThresholdSeconds для SlowQueries с введённым значением", async () => {
    const user = userEvent.setup();
    render(<InvestigationWizard />);
    await selectScenario(user, /Долгие запросы/i);

    const field = screen.getByLabelText(/Порог длительности запроса/i);
    await user.clear(field);
    await user.type(field, "3");

    await user.click(screen.getByRole("button", { name: /Запустить/i }));

    expect(mutateAsync).toHaveBeenCalledTimes(1);
    expect(mutateAsync).toHaveBeenCalledWith(
      expect.objectContaining({ scenario: "SlowQueries", slowQueryThresholdSeconds: 3 })
    );
  });

  it("НЕ шлёт slowQueryThresholdSeconds для сценария без порога (Блокировки)", async () => {
    const user = userEvent.setup();
    render(<InvestigationWizard />);
    await selectScenario(user, /^Управляемые блокировки/i);

    await user.click(screen.getByRole("button", { name: /Запустить/i }));

    expect(mutateAsync).toHaveBeenCalledTimes(1);
    const body = mutateAsync.mock.calls[0][0];
    expect(body).not.toHaveProperty("slowQueryThresholdSeconds");
    expect(body.scenario).toBe("Locks");
  });
});
