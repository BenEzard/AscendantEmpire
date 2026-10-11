import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";

import TechnologyTreeViewer from "../TechnologyTreeViewer";
import { mixedCatalogue } from "./technologyFixtures";

// The shell, driven through its injected catalogue. That prop is the seam that makes this testable at all — there is no network mocking in this project, so
// the self-loading path is not exercised here; what is, is that the pieces are wired together and that the overlay closes the way it should.
//
// Deliberately not tested: panning and zooming by real events. jsdom has no layout and no pointer capture, so the viewport maths is covered in
// useSvgViewport.test.ts instead.

function renderViewer(overrides: Partial<React.ComponentProps<typeof TechnologyTreeViewer>> = {}) {
  const onClose = vi.fn();

  // The viewer calls useTechnologyCatalogue unconditionally — switched off rather than skipped when a catalogue is injected — so it still needs a provider.
  const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false } } });

  render(
    <QueryClientProvider client={queryClient}>
      <TechnologyTreeViewer onClose={onClose} technologies={mixedCatalogue()} {...overrides} />
    </QueryClientProvider>,
  );

  return { onClose };
}

// ─── What it renders ────────────────────────────────────────────────────────────

describe("with a catalogue", () => {
  it("summarises the graph it is showing", () => {
    renderViewer();

    // Six technologies, eight prerequisite edges, two of them optional.
    expect(screen.getByText(/6 technologies · 8 prerequisites, 2 optional/)).toBeInTheDocument();
  });

  it("renders the canvas, the outline and the legend together", () => {
    renderViewer();

    expect(screen.getByTestId("technology-tree-canvas")).toBeInTheDocument();
    expect(screen.getByTestId("technology-tree-outline")).toBeInTheDocument();
    expect(screen.getByTestId("technology-tree-legend")).toBeInTheDocument();
  });

  it("describes the graph to assistive technology as one image with a summary", () => {
    renderViewer();

    expect(screen.getByRole("img", { name: /6 technologies, 8 prerequisite links across 4 columns/ })).toBeInTheDocument();
  });

  it("waits for a selection before filling the details panel", () => {
    renderViewer();

    expect(screen.getByText(/Pick a technology/)).toBeInTheDocument();
  });
});

// ─── Loading and failure ────────────────────────────────────────────────────────

describe("while loading", () => {
  it("says what it is waiting for", () => {
    renderViewer({ loading: true });

    expect(screen.getByText(/Loading the technology catalogue/)).toBeInTheDocument();
    expect(screen.queryByTestId("technology-tree-canvas")).not.toBeInTheDocument();
  });
});

describe("when the catalogue could not be read", () => {
  it("shows every message the server sent, and no canvas", () => {
    renderViewer({ errorMessages: ["Technology catalogue unavailable.", "Try again shortly."] });

    expect(screen.getByText("Technology catalogue unavailable.")).toBeInTheDocument();
    expect(screen.getByText("Try again shortly.")).toBeInTheDocument();
    expect(screen.queryByTestId("technology-tree-canvas")).not.toBeInTheDocument();
  });
});

// ─── Selecting ──────────────────────────────────────────────────────────────────

describe("selecting a technology", () => {
  it("fills the details panel from the outline rail", async () => {
    const user = userEvent.setup();

    renderViewer();
    await user.click(screen.getByRole("button", { name: /^Technology 4/ }));

    // The panel restates the two prerequisite kinds in words, which is the tree's line styles spelled out.
    expect(screen.getByText(/All of — every one must be discovered/)).toBeInTheDocument();
    expect(screen.getByText(/Any one of — one is enough/)).toBeInTheDocument();
  });

  it("marks the chosen row as current", async () => {
    const user = userEvent.setup();

    renderViewer();
    await user.click(screen.getByRole("button", { name: /^Technology 4/ }));

    expect(screen.getByRole("button", { name: /^Technology 4/ })).toHaveAttribute("aria-current", "true");
  });

  it("follows a prerequisite from the panel to that technology", async () => {
    const user = userEvent.setup();

    renderViewer();
    await user.click(screen.getByRole("button", { name: /^Technology 4/ }));
    await user.click(screen.getByRole("heading", { name: "Technology 4" }).closest("div")!.querySelector("button")!);

    // Any of the panel's prerequisite rows moves the selection; the heading follows it.
    expect(screen.getByRole("heading", { level: 2 })).not.toHaveTextContent("Technology 4");
  });
});

// ─── Closing ────────────────────────────────────────────────────────────────────

describe("closing", () => {
  it("closes on the close button", async () => {
    const user = userEvent.setup();
    const { onClose } = renderViewer();

    await user.click(screen.getByRole("button", { name: "Close the technology tree" }));

    expect(onClose).toHaveBeenCalled();
  });

  it("closes on Escape when nothing is selected", async () => {
    const user = userEvent.setup();
    const { onClose } = renderViewer();

    await user.keyboard("{Escape}");

    expect(onClose).toHaveBeenCalled();
  });

  it("clears the selection on Escape instead of closing, so one keypress never does both", async () => {
    const user = userEvent.setup();
    const { onClose } = renderViewer();

    await user.click(screen.getByRole("button", { name: /^Technology 4/ }));
    await user.keyboard("{Escape}");

    expect(onClose).not.toHaveBeenCalled();
    expect(screen.getByText(/Pick a technology/)).toBeInTheDocument();
  });
});

// ─── The outline toggle ─────────────────────────────────────────────────────────

describe("the outline rail", () => {
  it("can be collapsed to give the canvas the full width, and reports its state", async () => {
    const user = userEvent.setup();

    renderViewer();
    await user.click(screen.getByRole("button", { name: "Hide the technology list" }));

    expect(screen.queryByTestId("technology-tree-outline")).not.toBeInTheDocument();
    expect(screen.getByRole("button", { name: "Show the technology list" })).toHaveAttribute("aria-pressed", "false");
  });
});
