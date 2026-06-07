import { describe, expect, it } from "vitest";
import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import "@/i18n";
import { ExportMenu } from "../ExportMenu";
import type { LicenseUsageSeriesResponse } from "../../types";

const data: LicenseUsageSeriesResponse = {
  buckets: [
    { bucketStartUtc: "2026-06-01T12:00:00Z", consumedAvg: 3.5, consumedMax: 5, limit: 10 },
  ],
  fromUtc: "2026-06-01T12:00:00Z",
  toUtc: "2026-06-07T12:00:00Z",
  peakConsumed: 5,
  peakLimit: 10,
  peakAtUtc: "2026-06-01T12:00:00Z",
  averageConsumed: 3.5,
};

describe("ExportMenu", () => {
  it("renders the «Скачать» trigger when the series has data", () => {
    render(<ExportMenu data={data} scope="all" />);
    expect(screen.getByRole("button", { name: /Скачать/ })).toBeInTheDocument();
  });

  it("offers CSV, Excel, HTML and PDF items once opened", async () => {
    const user = userEvent.setup();
    render(<ExportMenu data={data} scope="all" />);
    await user.click(screen.getByRole("button", { name: /Скачать/ }));
    expect(await screen.findByRole("menuitem", { name: "CSV" })).toBeInTheDocument();
    expect(screen.getByRole("menuitem", { name: "Excel" })).toBeInTheDocument();
    expect(screen.getByRole("menuitem", { name: /HTML/ })).toBeInTheDocument();
    expect(screen.getByRole("menuitem", { name: "PDF" })).toBeInTheDocument();
  });

  it("is hidden for an undefined series", () => {
    const { container } = render(<ExportMenu data={undefined} scope="all" />);
    expect(container).toBeEmptyDOMElement();
  });

  it("is hidden for an empty series", () => {
    const { container } = render(<ExportMenu data={{ ...data, buckets: [] }} scope="all" />);
    expect(container).toBeEmptyDOMElement();
  });
});
