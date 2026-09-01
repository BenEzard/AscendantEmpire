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
    hasMovementRemaining: true,
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

    const counts = screen.getAllByRole("button").map((button) => button.textContent);

    // Healthy plus injured — the soldiers still standing, not the strength the force was created at.
    expect(counts).toEqual(["100", "25"]);
  });

  /**
   * Forces the player can still act with come first, so a scrolling bar shows the useful ones without hunting.
   */
  it("orders forces with movement remaining ahead of those without", () => {
    renderToolbar({
      tile: tile([
        force({ id: 1, hasMovementRemaining: false }),
        force({ id: 2, hasMovementRemaining: true }),
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

    expect(onSelect).toHaveBeenCalledExactlyOnceWith(clicked);
  });

  it("marks the selected force", () => {
    renderToolbar({ tile: tile([force({ id: 1 }), force({ id: 2 })]), selectedForceId: 2 });

    expect(screen.getByRole("button", { name: /Force #1/ })).toHaveAttribute("aria-pressed", "false");
    expect(screen.getByRole("button", { name: /Force #2/ })).toHaveAttribute("aria-pressed", "true");
  });

  it("fades a force with no movement remaining", () => {
    renderToolbar({
      tile: tile([force({ id: 1, hasMovementRemaining: true }), force({ id: 2, hasMovementRemaining: false })]),
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
    const spentForce = force({ id: 1, hasMovementRemaining: false });

    renderToolbar({ tile: tile([spentForce]), onSelect });

    const button = screen.getByRole("button", { name: /Force #1/ });

    expect(button).not.toBeDisabled();
    expect(button).not.toHaveAttribute("aria-disabled");

    await user.click(button);

    expect(onSelect).toHaveBeenCalledExactlyOnceWith(spentForce);
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

  it("shows no checkboxes unless selection is required", () => {
    renderToolbar({ tile: tile([force({ id: 1 }), force({ id: 2 })]) });

    expect(screen.queryAllByRole("checkbox")).toHaveLength(0);
  });

  it("shows a checkbox per force when selection is required, reflecting what is checked", () => {
    renderToolbar({
      tile: tile([force({ id: 1 }), force({ id: 2 })]),
      selectionRequired: true,
      checkedForceIds: new Set([2]),
    });

    const checkboxes = screen.getAllByRole("checkbox");

    expect(checkboxes).toHaveLength(2);
    expect(checkboxes[0]).not.toBeChecked();
    expect(checkboxes[1]).toBeChecked();
  });

  it("reports a force being checked", async () => {
    const user = userEvent.setup();
    const onCheckedChange = vi.fn();

    renderToolbar({
      tile: tile([force({ id: 1 })]),
      selectionRequired: true,
      checkedForceIds: new Set(),
      onCheckedChange,
    });

    await user.click(screen.getByRole("checkbox"));

    expect(onCheckedChange).toHaveBeenCalledExactlyOnceWith(1, true);
  });

  /**
   * The tick and the selection are separate channels. Split and join tick the forces they want to act on, which
   * must not drag the selection around with them — hence the checkbox being a sibling of the button, not a child.
   */
  it("does not change the selection when a checkbox is clicked", async () => {
    const user = userEvent.setup();
    const onSelect = vi.fn();

    renderToolbar({
      tile: tile([force({ id: 1 })]),
      selectionRequired: true,
      checkedForceIds: new Set(),
      onSelect,
      onCheckedChange: () => {},
    });

    await user.click(screen.getByRole("checkbox"));

    expect(onSelect).not.toHaveBeenCalled();
  });
});
