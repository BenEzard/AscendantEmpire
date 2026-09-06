import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";

import ConfirmDialog from "../ConfirmDialog";

// The point of this component over window.confirm is that the safe answer is the one that happens by accident,
// and that the whole thing is reachable by a test at all — so that is what these cover.

describe("answering", () => {
  it("confirms only when the confirming button is pressed", async () => {
    const user = userEvent.setup();
    const onConfirm = vi.fn();
    const onCancel = vi.fn();

    render(
      <ConfirmDialog open title="Delete game" message="Really?" confirmLabel="Delete" onConfirm={onConfirm} onCancel={onCancel} />,
    );

    await user.click(screen.getByRole("button", { name: "Delete" }));

    expect(onConfirm).toHaveBeenCalledOnce();
    expect(onCancel).not.toHaveBeenCalled();
  });

  it("treats Escape as a cancel, because a stray keypress must never destroy anything", async () => {
    const user = userEvent.setup();
    const onConfirm = vi.fn();
    const onCancel = vi.fn();

    render(<ConfirmDialog open title="Delete game" message="Really?" onConfirm={onConfirm} onCancel={onCancel} />);

    await user.keyboard("{Escape}");

    expect(onCancel).toHaveBeenCalledOnce();
    expect(onConfirm).not.toHaveBeenCalled();
  });

  it("renders nothing while closed, so a dialog left mounted cannot be answered by accident", () => {
    render(<ConfirmDialog open={false} title="Delete game" message="Really?" onConfirm={vi.fn()} onCancel={vi.fn()} />);

    expect(screen.queryByText("Really?")).not.toBeInTheDocument();
  });
});

describe("in-flight and failed states", () => {
  it("disables both buttons while the confirmed action is running, so it cannot be fired twice", () => {
    render(<ConfirmDialog open busy title="Delete game" message="Really?" onConfirm={vi.fn()} onCancel={vi.fn()} />);

    expect(screen.getByRole("button", { name: "Working..." })).toBeDisabled();
    expect(screen.getByRole("button", { name: "Cancel" })).toBeDisabled();
  });

  it("shows an error alongside the question, so a failed attempt leaves the dialog usable rather than closing", () => {
    render(
      <ConfirmDialog open title="Delete game" message="Really?" error="The server said no." onConfirm={vi.fn()} onCancel={vi.fn()} />,
    );

    expect(screen.getByText("The server said no.")).toBeInTheDocument();
    expect(screen.getByText("Really?")).toBeInTheDocument();
  });
});
