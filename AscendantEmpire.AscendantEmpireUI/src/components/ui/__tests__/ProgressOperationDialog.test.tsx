import { act, render, screen } from "@testing-library/react";

import ProgressOperationDialog from "../ProgressOperationDialog";

// A completed operation has nothing left to cancel, so these pin the two things that follow from that:
// the footer button stops offering a cancel, and the dialog gets itself off the screen — but only when the
// operation actually succeeded, and only once however often the parent re-renders underneath it.

const AUTO_CLOSE_DELAY_MS = 3000;

beforeEach(() => {
  vi.useFakeTimers();
});

afterEach(() => {
  vi.useRealTimers();
});

/** Advances past the auto-close delay inside act, so the resulting state update is flushed. */
function runOutTheAutoCloseDelay() {
  act(() => {
    vi.advanceTimersByTime(AUTO_CLOSE_DELAY_MS);
  });
}

describe("while the operation is running", () => {
  it("offers a cancel and stays open, because a part-finished operation is still abandonable", () => {
    const onClose = vi.fn();

    render(
      <ProgressOperationDialog open title="Seeding" operationLabel="Working…" progressPercent={40} status="active" onClose={onClose} />,
    );

    expect(screen.getByRole("button", { name: "Cancel" })).toBeInTheDocument();

    runOutTheAutoCloseDelay();

    expect(onClose).not.toHaveBeenCalled();
  });
});

describe("when the operation completes", () => {
  it("relabels the button Complete once the bar is full, so it no longer offers a cancel that would do nothing", () => {
    render(
      <ProgressOperationDialog open title="Seeding" operationLabel="Working…" progressPercent={100} status="active" onClose={vi.fn()} />,
    );

    expect(screen.getByRole("button", { name: "Complete" })).toBeInTheDocument();
    expect(screen.queryByRole("button", { name: "Cancel" })).not.toBeInTheDocument();
  });

  it("closes itself after the delay rather than leaving a finished dialog on screen", () => {
    const onClose = vi.fn();

    render(
      <ProgressOperationDialog open title="Seeding" operationLabel="Working…" progressPercent={100} status="active" onClose={onClose} />,
    );

    expect(onClose).not.toHaveBeenCalled();

    runOutTheAutoCloseDelay();

    expect(onClose).toHaveBeenCalledOnce();
  });

  it("closes itself on a reported done as well, where the caller flips status instead of reaching 100", () => {
    const onClose = vi.fn();

    render(
      <ProgressOperationDialog open title="Seeding" operationLabel="Working…" progressPercent={0} status="done" successMessage="Seeded." onClose={onClose} />,
    );

    expect(screen.getByRole("button", { name: "Complete" })).toBeInTheDocument();

    runOutTheAutoCloseDelay();

    expect(onClose).toHaveBeenCalledOnce();
  });

  it("keeps counting down across a re-render with a fresh onClose, since callers pass an inline arrow on every poll tick", () => {
    const onClose = vi.fn();
    const props = { open: true, title: "Seeding", operationLabel: "Working…", progressPercent: 100, status: "active" as const };

    const { rerender } = render(<ProgressOperationDialog {...props} onClose={() => onClose()} />);

    // Half way through the countdown, re-render the way a polling parent would.
    act(() => {
      vi.advanceTimersByTime(AUTO_CLOSE_DELAY_MS / 2);
    });
    rerender(<ProgressOperationDialog {...props} onClose={() => onClose()} />);

    runOutTheAutoCloseDelay();

    expect(onClose).toHaveBeenCalledOnce();
  });
});

describe("when the operation fails", () => {
  it("never closes itself and never says Complete, so the error survives long enough to be read", () => {
    const onClose = vi.fn();

    render(
      <ProgressOperationDialog open title="Seeding" operationLabel="Working…" progressPercent={100} status="error" errorMessage="The server said no." onClose={onClose} />,
    );

    // The modal's own × carries aria-label "Close" too, so match the footer button by its visible text.
    expect(screen.getByText("Close", { selector: "button" })).toBeInTheDocument();
    expect(screen.queryByRole("button", { name: "Complete" })).not.toBeInTheDocument();
    expect(screen.getByText("The server said no.")).toBeInTheDocument();

    runOutTheAutoCloseDelay();

    expect(onClose).not.toHaveBeenCalled();
  });
});

describe("while closed", () => {
  it("does not fire onClose, so a dialog left mounted cannot close a later one", () => {
    const onClose = vi.fn();

    render(
      <ProgressOperationDialog open={false} title="Seeding" operationLabel="Working…" progressPercent={100} status="done" onClose={onClose} />,
    );

    runOutTheAutoCloseDelay();

    expect(onClose).not.toHaveBeenCalled();
  });
});
