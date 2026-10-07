// The shared ToolBar's trailing region: that it renders what it is given, renders nothing when it is not, and
// changes neither the presence nor the order of the button groups either way.
//
// That last part is the whole point of pinning the region with `margin-left: auto` rather than reordering the
// container, so it is what these tests hold still.

import { render, screen } from "@testing-library/react";
import ToolBar, { type ToolBarGroup } from "../toolbar/Toolbar";

const groups: ToolBarGroup[] = [
  { caption: "Game", buttons: [{ key: "new", tooltip: "New game", icon: <span>N</span> }] },
  { caption: "Structures", buttons: [{ key: "end", tooltip: "End turn", icon: <span>E</span> }] },
];

/** The captions of the button groups, in the order they are laid out. */
function groupCaptions(): string[] {
  return Array.from(document.querySelectorAll(".toolbar-caption")).map((caption) => caption.textContent ?? "");
}

describe("the trailing region", () => {
  it("renders the content it is given", () => {
    render(<ToolBar groups={groups} trailing={<span>Empress Aelara</span>} />);

    expect(screen.getByText("Empress Aelara")).toBeInTheDocument();
    expect(document.querySelector(".toolbar-trailing")).not.toBeNull();
  });

  it("renders no region at all when none is given, so existing callers are unchanged", () => {
    render(<ToolBar groups={groups} />);

    expect(document.querySelector(".toolbar-trailing")).toBeNull();
  });

  it("leaves the button groups and their order exactly as they are", () => {
    const { unmount } = render(<ToolBar groups={groups} />);
    const withoutTrailing = groupCaptions();
    unmount();

    render(<ToolBar groups={groups} trailing={<span>Empress Aelara</span>} />);

    expect(groupCaptions()).toEqual(withoutTrailing);
    expect(groupCaptions()).toEqual(["Game", "Structures"]);
  });
});
