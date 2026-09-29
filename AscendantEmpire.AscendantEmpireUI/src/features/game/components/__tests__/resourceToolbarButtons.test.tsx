import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import ToolBar from "@/components/ui/toolbar/Toolbar";
import type { PlayerResourceMetaCategory } from "@/features/game/api/playerResourcesApi";
import { resourceToolbarButtonDomId } from "@/features/game/hooks/useResourcePanels";
import { buildResourceToolbarButtons } from "../resourceToolbarButtons";

function aMetaCategory(overrides: Partial<PlayerResourceMetaCategory> = {}): PlayerResourceMetaCategory {
  return {
    id: 1,
    code: "ConstructionMaterials",
    displayName: "Construction Materials",
    iconPath: "/resources/wood/oak-log_40px.png",
    sortOrder: 1,
    categories: [],
    ...overrides,
  };
}

const CONSTRUCTION = aMetaCategory();
const WEAPONS = aMetaCategory({ id: 2, code: "Weapons", displayName: "Weapons", iconPath: "/resources/unit-design/weapons/melee/stone-axe_40px.png", sortOrder: 2 });

function renderButtons(metaCategories: PlayerResourceMetaCategory[], openCode: string | null, onToggle: (code: string) => void = () => {}) {
  return render(<ToolBar groups={[{ caption: "Resources", buttons: buildResourceToolbarButtons(metaCategories, openCode, onToggle) }]} />);
}

/**
 * RS-12: the Resources group is one button per unlocked meta-category, so what used to be a single hard-coded button is now data — and the button's id
 * is what its panel anchors to.
 */
describe("buildResourceToolbarButtons", () => {
  it("renders one button per meta-category, in the order the API returned them", () => {
    renderButtons([CONSTRUCTION, WEAPONS], null);

    const buttons = screen.getAllByRole("button");
    expect(buttons.map((button) => button.getAttribute("aria-label") ?? button.textContent)).toHaveLength(2);
    expect(screen.getByRole("button", { name: "Construction Materials" })).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "Weapons" })).toBeInTheDocument();
  });

  it("gives each button the DOM id its panel anchors to", () => {
    renderButtons([CONSTRUCTION, WEAPONS], null);

    expect(screen.getByRole("button", { name: "Construction Materials" }).id).toBe(resourceToolbarButtonDomId("ConstructionMaterials"));
    expect(screen.getByRole("button", { name: "Weapons" }).id).toBe(resourceToolbarButtonDomId("Weapons"));
  });

  it("renders the meta-category's catalogue icon", () => {
    renderButtons([CONSTRUCTION], null);

    expect(screen.getByRole("img", { name: "Construction Materials" })).toHaveAttribute("src", CONSTRUCTION.iconPath);
  });

  it("keeps the button nameable when the meta-category has no icon", () => {
    renderButtons([aMetaCategory({ iconPath: "" })], null);

    expect(screen.getByRole("button", { name: "Construction Materials" })).toBeInTheDocument();
  });

  it("presses the button whose panel is open", () => {
    renderButtons([CONSTRUCTION, WEAPONS], "Weapons");

    expect(screen.getByRole("button", { name: "Weapons" })).toHaveAttribute("aria-pressed", "true");
    expect(screen.getByRole("button", { name: "Construction Materials" })).toHaveAttribute("aria-pressed", "false");
  });

  it("toggles the clicked meta-category's panel by code", async () => {
    const user = userEvent.setup();
    const onToggle = vi.fn();
    renderButtons([CONSTRUCTION, WEAPONS], null, onToggle);

    await user.click(screen.getByRole("button", { name: "Weapons" }));

    expect(onToggle).toHaveBeenCalledWith("Weapons");
  });

  it("renders no buttons for a player with nothing unlocked", () => {
    renderButtons([], null);

    expect(screen.queryAllByRole("button")).toHaveLength(0);
  });
});
