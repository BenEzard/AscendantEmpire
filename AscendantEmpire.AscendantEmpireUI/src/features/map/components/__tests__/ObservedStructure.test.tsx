import { render } from "@testing-library/react";

import ObservedStructure from "../ObservedStructure";
import type { ObservedStructureTile } from "../TileTypes";

/** A village standing complete on a tile, with its name, unless a test says otherwise. */
function structure(overrides: Partial<ObservedStructureTile> = {}): ObservedStructureTile {
  return {
    id: 91,
    structureId: 2,
    structureCode: "village",
    settlementName: "Riverhold",
    positionOnTile: 1,
    completionPercent: 100,
    ...overrides,
  };
}

describe("ObservedStructure", () => {
  it("draws the sprite for the structure's code", () => {
    const { container } = render(<ObservedStructure structure={structure()} />);

    expect(container.querySelector(".observed-structure__image")).not.toBeNull();
  });

  /**
   * The code is what the artwork is keyed on, and the catalogue can hold kinds the client has no sprite for. Drawing nothing is
   * deliberate: a placeholder on the tile would read as a structure the player could act on.
   */
  it("draws nothing for a kind it has no art for", () => {
    const { container } = render(<ObservedStructure structure={structure({ structureCode: "aqueduct" })} />);

    expect(container.querySelector(".observed-structure")).toBeNull();
  });

  /** The name belongs to SettlementLabelOverlay, which draws over the whole grid because a name is wider than its tile. */
  it("draws no name, which is the overlay's to draw", () => {
    const { container } = render(<ObservedStructure structure={structure()} />);

    expect(container.textContent).toBe("");
  });

  /** The bar is how a settlement under construction is told apart from a finished one, so it shows the progress and then goes. */
  it("draws the completion bar only while the structure is unfinished", () => {
    const { container: building } = render(<ObservedStructure structure={structure({ completionPercent: 50 })} />);

    expect(building.querySelector<HTMLElement>(".observed-structure__completion-fill")?.style.height).toBe("50%");

    const { container: finished } = render(<ObservedStructure structure={structure()} />);

    expect(finished.querySelector(".observed-structure__completion-bar")).toBeNull();
  });

});
