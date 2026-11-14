import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";

import SettlementManagerDialog from "../SettlementManagerDialog";

// The dialog is still a shell: its pages hold placeholders, so what is pinned here is the frame — the title, the pages the nav
// offers, and the two things the dialog was asked for, a large panel whose size does not move as the player pages through it.

function renderDialog(overrides: Partial<React.ComponentProps<typeof SettlementManagerDialog>> = {}) {
  const onClose = vi.fn();

  render(<SettlementManagerDialog show settlementName="Ravenhold" onClose={onClose} {...overrides} />);

  return { onClose };
}

/** The content region PagedDialog pins when it is given a height — the 12-column row holding the nav and the open page. */
function contentRegion(): HTMLElement {
  return document.querySelector<HTMLElement>(".grid-cols-12")!;
}

describe("SettlementManagerDialog", () => {
  it("names the settlement in its title", () => {
    renderDialog();

    expect(screen.getByText("Settlement Manager: Ravenhold")).toBeInTheDocument();
  });

  /** Opened from a tile rather than a toolbar, so it may be mounted before any settlement has been double clicked. */
  it("falls back to its own name while it has no settlement", () => {
    renderDialog({ settlementName: null });

    expect(screen.getByText("Settlement Manager")).toBeInTheDocument();
  });

  it("stays closed when it is not shown", () => {
    renderDialog({ show: false });

    expect(screen.queryByText(/Settlement Manager/)).not.toBeInTheDocument();
  });

  /** PagedDialog numbers every tab and renders the number and label as adjacent spans, so the accessible name comes out as "1Overview". */
  it("offers a page per section of the settlement", () => {
    renderDialog();

    for (const label of ["Overview", "Population", "Production", "Buildings"]) {
      expect(screen.getByRole("button", { name: new RegExp(`^\\d+${label}$`) })).toBeInTheDocument();
    }
  });

  it("opens on the Overview page", () => {
    renderDialog();

    expect(screen.getByRole("button", { name: /^\d+Overview$/ })).toHaveAttribute("aria-current", "page");
  });

  /** "Large" was the ask: the widest panel Modal offers. */
  it("takes the widest panel available", () => {
    renderDialog();

    expect(document.querySelector(".max-w-6xl")).toBeInTheDocument();
  });

  /** The other half of the ask: a page with more on it than another must not stretch the dialog, so the content region is pinned rather than sized to fit. */
  it("pins its content region to a fixed height", () => {
    renderDialog();

    expect(contentRegion().style.height).not.toBe("");
  });

  it("keeps that height when the player pages through it", async () => {
    renderDialog();

    const heightOnOpen = contentRegion().style.height;

    await userEvent.click(screen.getByRole("button", { name: /^\d+Buildings$/ }));

    expect(contentRegion().style.height).toBe(heightOnOpen);
  });

  /** Two ways out, which share an accessible name: the footer button and the panel's own ×. */
  it("closes on either of its close controls", async () => {
    const { onClose } = renderDialog();

    for (const control of screen.getAllByRole("button", { name: "Close" })) {
      await userEvent.click(control);
    }

    expect(onClose).toHaveBeenCalledTimes(2);
  });
});
