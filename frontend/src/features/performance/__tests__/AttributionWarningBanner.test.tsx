import { render, screen } from "@testing-library/react";
import { describe, expect, it } from "vitest";
import "@/i18n";
import { AttributionWarningBanner } from "../AttributionWarningBanner";

describe("AttributionWarningBanner", () => {
  it("names the inaccessible count and points at admin rights (honest signal, MLC-064a)", () => {
    render(<AttributionWarningBanner processesInaccessible={7} />);

    expect(screen.getByText(/Недоступно процессов: 7/)).toBeInTheDocument();
    expect(screen.getByText(/правами администратора/)).toBeInTheDocument();
  });
});
