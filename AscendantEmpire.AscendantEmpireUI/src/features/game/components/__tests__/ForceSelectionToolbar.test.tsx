import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import type { ObservedForceTile } from "@/features/map/components/TileTypes";
import ForceSelectionToolbar, { type ForceSelectionToolbarTile } from "../ForceSelectionToolbar";

// ObservedForce returns null outright when it can't resolve an image for the template, so without this map every
// entry would render empty and the marker assertions below would pass against nothing.
const UNIT_IMAGE_SRC_BY_TEMPLATE_ID = new Map([[1, "/settlers_40px.png"]]);

// The bar scrolls the selected entry into view on mount; jsdom doesn't implement scrollIntoView.
beforeAll(() => {
  Element.prototype.scrollIntoView = vi.fn();
});

/** Builds a force, defaulting to one at full strength with movement left. */
function force(overrides: Partial<ObservedForceTile> = {}): ObservedForceTile {
  return {
    id: 1,
    recordType: 1,
    ownerPlayerId: 1,
    unitTemplateId: 1,
    healthyTroops: 80,
    injuredTroops: 20,
    initialStrength: 100,
    hasCampaignMovementRemaining: true,
    campaignMovementRemaining: 2,
    campaignMovementPoints: 2,
    // Nothing spends battle movement yet, so the bar never shows it; null is its masked value.
    battleMovementRemaining: null,
    ...overrides,
  };
}

function tile(forces: ObservedForceTile[]): ForceSelectionToolbarTile {
  return { x: 3, y: 4, forces };
}

/** Renders the bar with the props that are the same in almost every case. */
function renderToolbar(props: Partial<React.ComponentProps<typeof ForceSelectionToolbar>> = {}) {
  return render(
    <ForceSelectionToolbar
      visible
      tile={tile([force()])}
      unitImageSrcByUnitTemplateId={UNIT_IMAGE_SRC_BY_TEMPLATE_ID}
      onSelect={() => {}}
      {...props}
    />,
  );
}

/** The marker span, which is what carries the spent-movement fade. */
function markerFor(button: HTMLElement): HTMLElement {
  return button.querySelector(".force-selection-toolbar__marker") as HTMLElement;
}

describe("ForceSelectionToolbar", () => {
  it("renders nothing when not visible", () => {
    renderToolbar({ visible: false });

    expect(screen.queryByLabelText("Force selection toolbar")).not.toBeInTheDocument();
  });

  it("renders nothing without a tile", () => {
    renderToolbar({ tile: null });

    expect(screen.queryByLabelText("Force selection toolbar")).not.toBeInTheDocument();
  });

  it("renders nothing when the tile holds no forces", () => {
    renderToolbar({ tile: tile([]) });

    expect(screen.queryByLabelText("Force selection toolbar")).not.toBeInTheDocument();
  });

  it("renders one entry per force showing its current strength", () => {
    renderToolbar({
      tile: tile([
        force({ id: 1, healthyTroops: 80, injuredTroops: 20 }),
        force({ id: 2, healthyTroops: 15, injuredTroops: 10 }),
      ]),
    });

    // The troop caption specifically, not the whole button: the button also carries the movement figure, and this
    // test is about strength.
    const counts = screen
      .getAllByRole("button")
      .map((button) => button.querySelector(".force-selection-toolbar__troops")?.textContent);

    // Healthy plus injured — the soldiers still standing, not the strength the force was created at.
    expect(counts).toEqual(["100", "25"]);
  });

  it("shows what movement each force has left, out of a full turn", () => {
    renderToolbar({
      tile: tile([force({ id: 1, campaignMovementRemaining: 1.5, campaignMovementPoints: 2 })]),
    });

    const movement = screen.getByRole("button").querySelector(".force-selection-toolbar__movement");

    // Whole numbers lose their trailing zero, so "2" rather than "2.0" — costs are authored to one decimal place.
    expect(movement?.textContent).toBe("1.5 / 2 MP");
  });

  it("shows no movement figure for a force whose numbers the API masked", () => {
    // Enemy forces come back with these nulled out rather than revealing how far they could still travel, and there
    // is no honest placeholder to show in their place.
    renderToolbar({
      tile: tile([force({ id: 1, campaignMovementRemaining: null, campaignMovementPoints: null })]),
    });

    expect(screen.getByRole("button").querySelector(".force-selection-toolbar__movement")).toBeNull();
  });

  it("puts the movement figure in the button's accessible name, where the visible caption is decorative", () => {
    renderToolbar({
      tile: tile([force({ id: 1, campaignMovementRemaining: 1.5, campaignMovementPoints: 2 })]),
    });

    expect(screen.getByRole("button")).toHaveAccessibleName(expect.stringContaining("1.5 / 2 movement points"));
  });

  /**
   * Forces the player can still act with come first, so a scrolling bar shows the useful ones without hunting.
   */
  it("orders forces with movement remaining ahead of those without", () => {
    renderToolbar({
      tile: tile([
        force({ id: 1, hasCampaignMovementRemaining: false }),
        force({ id: 2, hasCampaignMovementRemaining: true }),
      ]),
    });

    const names = screen.getAllByRole("button").map((button) => button.getAttribute("aria-label"));

    expect(names).toEqual([expect.stringContaining("Force #2"), expect.stringContaining("Force #1")]);
  });

  /**
   * The id tiebreak is what makes the order stable: the forces arrive in whatever order the query returned them,
   * and without a second key the buttons would shuffle under the cursor on every refetch.
   */
  it("orders forces by id within a movement group regardless of the order supplied", () => {
    renderToolbar({
      tile: tile([force({ id: 7 }), force({ id: 2 }), force({ id: 5 })]),
    });

    const names = screen.getAllByRole("button").map((button) => button.getAttribute("aria-label"));

    expect(names).toEqual([
      expect.stringContaining("Force #2"),
      expect.stringContaining("Force #5"),
      expect.stringContaining("Force #7"),
    ]);
  });

  it("selects a force when its entry is clicked", async () => {
    const user = userEvent.setup();
    const onSelect = vi.fn();
    const clicked = force({ id: 2 });

    renderToolbar({ tile: tile([force({ id: 1 }), clicked]), onSelect });

    await user.click(screen.getByRole("button", { name: /Force #2/ }));

    // An unmodified click reports append: false, which is what makes it replace the selection rather than add to it.
    expect(onSelect).toHaveBeenCalledExactlyOnceWith(clicked, false);
  });

  /**
   * The bar reports the gesture, not the intent: it says the click was modified and leaves the caller to decide
   * that this means "add to the selection, or take it back out".
   */
  it("reports a ctrl+click as an appending click", async () => {
    const user = userEvent.setup();
    const onSelect = vi.fn();
    const clicked = force({ id: 2 });

    renderToolbar({ tile: tile([force({ id: 1 }), clicked]), onSelect });

    await user.keyboard("{Control>}");
    await user.click(screen.getByRole("button", { name: /Force #2/ }));
    await user.keyboard("{/Control}");

    expect(onSelect).toHaveBeenCalledExactlyOnceWith(clicked, true);
  });

  /** On macOS ctrl+click is the system context-menu gesture, so ⌘ is what a Mac player reaches for instead. */
  it("reports a meta+click as an appending click", async () => {
    const user = userEvent.setup();
    const onSelect = vi.fn();
    const clicked = force({ id: 2 });

    renderToolbar({ tile: tile([force({ id: 1 }), clicked]), onSelect });

    await user.keyboard("{Meta>}");
    await user.click(screen.getByRole("button", { name: /Force #2/ }));
    await user.keyboard("{/Meta}");

    expect(onSelect).toHaveBeenCalledExactlyOnceWith(clicked, true);
  });

  it("marks the selected force", () => {
    renderToolbar({ tile: tile([force({ id: 1 }), force({ id: 2 })]), selectedForceIds: new Set([2]) });

    expect(screen.getByRole("button", { name: /Force #1/ })).toHaveAttribute("aria-pressed", "false");
    expect(screen.getByRole("button", { name: /Force #2/ })).toHaveAttribute("aria-pressed", "true");
  });

  /** Several forces can be selected at once, and every one of them has to read as pressed. */
  it("marks every selected force when several are selected", () => {
    renderToolbar({
      tile: tile([force({ id: 1 }), force({ id: 2 }), force({ id: 3 })]),
      selectedForceIds: new Set([1, 3]),
    });

    expect(screen.getByRole("button", { name: /Force #1/ })).toHaveAttribute("aria-pressed", "true");
    expect(screen.getByRole("button", { name: /Force #2/ })).toHaveAttribute("aria-pressed", "false");
    expect(screen.getByRole("button", { name: /Force #3/ })).toHaveAttribute("aria-pressed", "true");
  });

  /** Nothing selected is a real state now — ctrl+clicking the last force off leaves the bar showing exactly this. */
  it("marks no force as pressed when the selection is empty", () => {
    renderToolbar({ tile: tile([force({ id: 1 }), force({ id: 2 })]), selectedForceIds: new Set() });

    for (const button of screen.getAllByRole("button")) {
      expect(button).toHaveAttribute("aria-pressed", "false");
    }
  });

  it("fades a force with no movement remaining", () => {
    renderToolbar({
      tile: tile([force({ id: 1, hasCampaignMovementRemaining: true }), force({ id: 2, hasCampaignMovementRemaining: false })]),
    });

    // Asserted on the class, not the computed style: jsdom loads no stylesheet, so filter would never compute.
    expect(markerFor(screen.getByRole("button", { name: /Force #1/ }))).not.toHaveClass("force-selection-toolbar__marker--spent");
    expect(markerFor(screen.getByRole("button", { name: /Force #2/ }))).toHaveClass("force-selection-toolbar__marker--spent");
  });

  /**
   * A spent force is faded, not disabled. The player still has to be able to select it — not least to interrupt
   * whatever orders it is already carrying out.
   */
  it("still selects a force that has no movement remaining", async () => {
    const user = userEvent.setup();
    const onSelect = vi.fn();
    const spentForce = force({ id: 1, hasCampaignMovementRemaining: false });

    renderToolbar({ tile: tile([spentForce]), onSelect });

    const button = screen.getByRole("button", { name: /Force #1/ });

    expect(button).not.toBeDisabled();
    expect(button).not.toHaveAttribute("aria-disabled");

    await user.click(button);

    expect(onSelect).toHaveBeenCalledExactlyOnceWith(spentForce, false);
  });

  it("names the unit in a tooltip on hover", async () => {
    const user = userEvent.setup();

    renderToolbar({
      tile: tile([force({ id: 1, unitTemplateId: 1 })]),
      unitNameByUnitTemplateId: new Map([[1, "Settler"]]),
    });

    await user.hover(screen.getByRole("button", { name: /Force #1/ }));

    expect(await screen.findByRole("tooltip")).toHaveTextContent("Settler");
  });

  /**
   * The names come from a separate query to the image map's, so there is a window where a force renders before its
   * template's name has arrived. No bubble beats an empty one.
   */
  it("shows no tooltip for a template whose name is not known", async () => {
    const user = userEvent.setup();

    renderToolbar({ tile: tile([force({ id: 1, unitTemplateId: 1 })]) });

    await user.hover(screen.getByRole("button", { name: /Force #1/ }));

    expect(screen.queryByRole("tooltip")).not.toBeInTheDocument();
  });

  /**
   * The bar once carried a checkbox per force as a second, separate channel for the forces an action should act
   * on. Ctrl+click made it redundant — the selection *is* that list now — so there is nothing left to tick.
   */
  it("shows no checkboxes", () => {
    renderToolbar({ tile: tile([force({ id: 1 }), force({ id: 2 })]), selectedForceIds: new Set([1]) });

    expect(screen.queryAllByRole("checkbox")).toHaveLength(0);
  });
});
