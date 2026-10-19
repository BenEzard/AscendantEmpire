import { render } from "@testing-library/react";

import SettlementLabelOverlay from "../SettlementLabelOverlay";
import type { ObservedStructureTile, TileCell } from "../TileTypes";

const TILE_SIZE_PX = 50;

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

/** A one-row grid of empty tiles, with whatever structures a test puts on them. */
function grid(structuresByX: Record<number, ObservedStructureTile[]>, width = 3): TileCell[][] {
  return [
    Array.from({ length: width }, (_unused, x) => ({ x, y: 0, structures: structuresByX[x] }) as TileCell),
  ];
}

function labels(container: HTMLElement) {
  return [...container.querySelectorAll<HTMLElement>(".settlement-label")];
}

describe("SettlementLabelOverlay", () => {
  it("draws one label per settlement on the grid", () => {
    const { container } = render(
      <SettlementLabelOverlay
        tiles2d={grid({ 0: [structure()], 2: [structure({ id: 92, settlementName: "Stonegate" })] })}
        width={3}
        height={1}
        tileSizePx={TILE_SIZE_PX}
      />,
    );

    expect(labels(container).map((label) => label.textContent)).toEqual(["Riverhold", "Stonegate"]);
  });

  /**
   * The centre of the tile horizontally, its bottom edge vertically. The CSS pulls the label back by its own width and height
   * from there, which is what keeps a name longer than its tile centred on the settlement rather than running off to one side.
   */
  it("anchors a label to the centre of the bottom edge of its tile", () => {
    const { container } = render(
      <SettlementLabelOverlay tiles2d={grid({ 1: [structure()] })} width={3} height={1} tileSizePx={TILE_SIZE_PX} />,
    );

    const [label] = labels(container);

    expect(label.style.left).toBe("75px");
    expect(label.style.top).toBe("50px");
  });

  /** A watch tower is a kind, not a place, so it carries no name and must not be labelled with one. */
  it("ignores a structure that is nobody's settlement", () => {
    const { container } = render(
      <SettlementLabelOverlay
        tiles2d={grid({ 0: [structure({ settlementName: null })] })}
        width={3}
        height={1}
        tileSizePx={TILE_SIZE_PX}
      />,
    );

    expect(labels(container)).toHaveLength(0);
  });

  /** Nothing to label means nothing in the tree at all, rather than an empty layer sitting over the map. */
  it("draws nothing when no settlement is visible", () => {
    const { container } = render(
      <SettlementLabelOverlay tiles2d={grid({})} width={3} height={1} tileSizePx={TILE_SIZE_PX} />,
    );

    expect(container.querySelector(".settlement-labels")).toBeNull();
  });

  /** It covers the grid exactly, so the coordinates above land on the tiles they were worked out from. */
  it("covers the whole grid", () => {
    const { container } = render(
      <SettlementLabelOverlay tiles2d={grid({ 0: [structure()] })} width={3} height={1} tileSizePx={TILE_SIZE_PX} />,
    );

    const layer = container.querySelector<HTMLElement>(".settlement-labels");

    expect(layer?.style.width).toBe("150px");
    expect(layer?.style.height).toBe("50px");
  });
});
