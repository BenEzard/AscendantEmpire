import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";

import BattleDialog from "../BattleDialog";

// The dialog is a placeholder until the battle screen is designed, so all that is worth pinning is that it opens for the
// force it was asked about, stays out of the way while closed, and can be dismissed.

describe("BattleDialog", () => {
  it("names the force whose battle was opened", () => {
    render(<BattleDialog show forceId={42} onClose={vi.fn()} />);

    expect(screen.getByText("Force #42 is in battle.")).toBeInTheDocument();
  });

  it("renders nothing while closed", () => {
    render(<BattleDialog show={false} forceId={null} onClose={vi.fn()} />);

    expect(screen.queryByText(/is in battle/)).not.toBeInTheDocument();
  });

  it("closes from its close button", async () => {
    const user = userEvent.setup();
    const onClose = vi.fn();

    render(<BattleDialog show forceId={42} onClose={onClose} />);

    await user.click(screen.getByRole("button", { name: "Close" }));

    expect(onClose).toHaveBeenCalledOnce();
  });
});
