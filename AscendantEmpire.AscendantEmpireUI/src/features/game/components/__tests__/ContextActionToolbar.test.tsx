import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import ContextActionToolbar, { type ContextActionToolbarAction } from "../ContextActionToolbar";

/** Builds a toolbar action, defaulting to an enabled one. */
function action(overrides: Partial<ContextActionToolbarAction> = {}): ContextActionToolbarAction {
  return {
    key: "split-force",
    label: "Split Force",
    icon: <span aria-hidden="true" />,
    displayOrder: 1000,
    isEnabled: true,
    disabledReasons: [],
    onClick: () => {},
    ...overrides,
  };
}

describe("ContextActionToolbar", () => {
  it("renders nothing when there is no selection", () => {
    render(<ContextActionToolbar visible={false} actions={[action()]} />);

    expect(screen.queryByLabelText("Context action toolbar")).not.toBeInTheDocument();
  });

  it("renders an available action as a clickable button", async () => {
    const user = userEvent.setup();
    const onClick = vi.fn();

    render(<ContextActionToolbar visible actions={[action({ onClick })]} />);

    await user.click(screen.getByRole("button", { name: "Split Force" }));

    expect(onClick).toHaveBeenCalledOnce();
  });

  /**
   * An unavailable action stays on the toolbar rather than disappearing — the player should be able to see the
   * action exists and find out what's stopping it.
   */
  it("renders an unavailable action as a disabled button carrying its reasons", () => {
    render(
      <ContextActionToolbar
        visible
        actions={[action({ isEnabled: false, disabledReasons: ["Unit has already moved this turn.", "It is too small."] })]}
      />,
    );

    const button = screen.getByRole("button", { name: /Split Force/ });

    expect(button).toBeDisabled();
    expect(button).toHaveAccessibleName("Split Force — Unit has already moved this turn. It is too small.");
  });

  it("does not fire onClick for an unavailable action", async () => {
    const user = userEvent.setup();
    const onClick = vi.fn();

    render(<ContextActionToolbar visible actions={[action({ isEnabled: false, disabledReasons: ["Nope."], onClick })]} />);

    await user.click(screen.getByRole("button", { name: /Split Force/ }));

    expect(onClick).not.toHaveBeenCalled();
  });

  /**
   * The toolbar's order is fixed by displayOrder, not by the order actions were assembled in — they arrive from
   * two sources (the backend's list and the frontend-only affordances), so the caller's ordering can't be trusted.
   */
  it("orders actions by displayOrder regardless of the order supplied", () => {
    render(
      <ContextActionToolbar
        visible
        actions={[
          action({ key: "split-force", label: "Split Force", displayOrder: 1000 }),
          action({ key: "select-force", label: "Select Force", displayOrder: 100 }),
        ]}
      />,
    );

    const labels = screen.getAllByRole("button").map((button) => button.textContent);

    expect(labels).toEqual(["Select Force", "Split Force"]);
  });
});
