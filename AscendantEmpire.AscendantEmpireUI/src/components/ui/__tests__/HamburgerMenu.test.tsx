import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import HamburgerMenu, { type HamburgerMenuGroup } from "../HamburgerMenu";

/** Builds a one-item group, defaulting to a labelled one. */
function group(overrides: Partial<HamburgerMenuGroup> = {}): HamburgerMenuGroup {
  return {
    label: "Game",
    items: [{ key: "new", label: "New", onClick: () => {} }],
    ...overrides,
  };
}

/** Opens the menu by clicking its trigger, and returns the user-event session for further input. */
async function openMenu() {
  const user = userEvent.setup();
  await user.click(screen.getByRole("button", { name: "Game menu" }));
  return user;
}

describe("HamburgerMenu", () => {
  it("shows the header above the groups once opened", async () => {
    render(<HamburgerMenu title="Game menu" header="Ben" groups={[group()]} />);

    // The header belongs to the open menu, not the closed trigger.
    expect(screen.queryByText("Ben")).not.toBeInTheDocument();

    await openMenu();

    expect(screen.getByText("Ben")).toBeInTheDocument();
  });

  /**
   * The header is a plain label, so it must not become a target for the pointer or the keyboard —
   * arrow-key navigation should land on the first real item instead.
   */
  it("keeps the header out of keyboard navigation", async () => {
    render(<HamburgerMenu title="Game menu" header="Ben" groups={[group()]} />);

    const user = await openMenu();
    await user.keyboard("{ArrowDown}");

    expect(screen.getByRole("menuitem", { name: "New" })).toHaveFocus();
  });

  it("renders an unlabelled group's items without a heading", async () => {
    const logOut = group({ label: undefined, items: [{ key: "logout", label: "Log out", onClick: () => {} }] });
    render(<HamburgerMenu title="Game menu" groups={[group(), logOut]} />);

    await openMenu();

    // The labelled group still gets its heading; the unlabelled one contributes only its item.
    expect(screen.getByText("Game")).toBeInTheDocument();
    expect(screen.getByRole("menuitem", { name: "Log out" })).toBeInTheDocument();
    expect(screen.getByRole("group", { name: "Game" })).toBeInTheDocument();
  });

  /** Two unlabelled groups would previously have collided on `undefined` as their React key. */
  it("renders multiple unlabelled groups", async () => {
    const first = group({ label: undefined, items: [{ key: "a", label: "First", onClick: () => {} }] });
    const second = group({ label: undefined, items: [{ key: "b", label: "Second", onClick: () => {} }] });
    render(<HamburgerMenu title="Game menu" groups={[first, second]} />);

    await openMenu();

    expect(screen.getByRole("menuitem", { name: "First" })).toBeInTheDocument();
    expect(screen.getByRole("menuitem", { name: "Second" })).toBeInTheDocument();
  });

  it("still invokes an item's handler and closes the menu", async () => {
    const onClick = vi.fn();
    const logOut = group({ label: undefined, items: [{ key: "logout", label: "Log out", onClick }] });
    render(<HamburgerMenu title="Game menu" header="Ben" groups={[logOut]} />);

    const user = await openMenu();
    await user.click(screen.getByRole("menuitem", { name: "Log out" }));

    expect(onClick).toHaveBeenCalledOnce();
    expect(screen.queryByRole("menu")).not.toBeInTheDocument();
  });
});
