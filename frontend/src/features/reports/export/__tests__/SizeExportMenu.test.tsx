import { describe, expect, it } from "vitest";
import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import "@/i18n";
import { SizeExportMenu } from "../SizeExportMenu";
import type { SizeExportData } from "../sizeExport";

const data: SizeExportData = {
  scope: "all",
  points: [{ atUtc: "2026-06-01T12:00:00Z", dataBytes: 1000, logBytes: 200, totalBytes: 1200 }],
  fromUtc: "2026-06-01T12:00:00Z",
  toUtc: "2026-06-07T12:00:00Z",
  tenants: [],
  databases: [],
};

describe("SizeExportMenu", () => {
  it("renders the «Скачать» trigger when the series has data", () => {
    render(<SizeExportMenu data={data} />);
    expect(screen.getByRole("button", { name: /Скачать/ })).toBeInTheDocument();
  });

  it("offers CSV, Excel, HTML and PDF items once opened", async () => {
    const user = userEvent.setup();
    render(<SizeExportMenu data={data} />);
    await user.click(screen.getByRole("button", { name: /Скачать/ }));
    expect(await screen.findByRole("menuitem", { name: "CSV" })).toBeInTheDocument();
    expect(screen.getByRole("menuitem", { name: "Excel" })).toBeInTheDocument();
    expect(screen.getByRole("menuitem", { name: /HTML/ })).toBeInTheDocument();
    expect(screen.getByRole("menuitem", { name: "PDF" })).toBeInTheDocument();
  });

  it("is hidden for an undefined series", () => {
    const { container } = render(<SizeExportMenu data={undefined} />);
    expect(container).toBeEmptyDOMElement();
  });

  it("is hidden for an empty series", () => {
    const { container } = render(<SizeExportMenu data={{ ...data, points: [] }} />);
    expect(container).toBeEmptyDOMElement();
  });
});
