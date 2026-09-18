import { act, render, screen } from "@testing-library/react";
import TileSupportMarker, { type TileSupportMarkerProps } from "../TileSupportMarker";
import { formatSupportTurns, supportTurnsLabel } from "../formatSupportTurns";
import { createHoveredTileStore } from "@/features/map/hooks/useHoveredTileStore";
import type { TileSupport } from "@/features/map/api/tileSupportApi";

// The selected force stands on (1, 1). (2, 1), (3, 1), (6, 1), (7, 1) and (8, 1) are in sight with a level; (4, 1) is in sight with none; (5, 1) is out of
// sight, though a stale answer still holds a level for it.
const SUPPORT = new Map<string, TileSupport>([
  ["1,1", { supportLevel: "Plenty", turns: 3 }],
  ["2,1", { supportLevel: "OneTurn", turns: 1 }],
  ["3,1", { supportLevel: "Exhausted", turns: 0 }],
  ["5,1", { supportLevel: "Insufficient", turns: 0 }],
  ["6,1", { supportLevel: "Plenty", turns: null }],
  ["7,1", { supportLevel: "Insufficient", turns: 0 }],
  ["8,1", { supportLevel: "Plenty", turns: 4 }],
]);

const OBSERVED = new Set(["1,1", "2,1", "3,1", "4,1", "6,1", "7,1", "8,1"]);
const SELECTED_FORCE_TILES = new Set(["1,1"]);

function renderMarker(props?: Partial<TileSupportMarkerProps>) {
  const hoveredTileStore = createHoveredTileStore();

  render(
    <TileSupportMarker
      hoveredTileStore={hoveredTileStore}
      supportByTileKey={SUPPORT}
      observedTileKeys={OBSERVED}
      selectedForceTileKeys={SELECTED_FORCE_TILES}
      {...props}
    />,
  );

  const hover = (coords: { x: number; y: number } | null) => act(() => hoveredTileStore.set(coords));

  return { hover };
}

describe("TileSupportMarker", () => {
  it("draws nothing while the pointer is off the map", () => {
    renderMarker();

    expect(screen.queryByRole("img")).not.toBeInTheDocument();
  });

  /** Centred on the hovered tile, in the level's colour, and following the pointer from tile to tile. */
  it("draws the hovered tile's level at its centre and follows the pointer", () => {
    const { hover } = renderMarker();

    hover({ x: 2, y: 1 });

    const marker = screen.getByRole("img", { name: "Can support for 1 turn" });
    expect(marker).toHaveClass("tile-support-marker", "tile-support-marker--support-one-turn");
    expect(marker).toHaveStyle({ left: "125px", top: "75px" });

    hover({ x: 3, y: 1 });

    expect(screen.getByRole("img", { name: "No food left" })).toHaveClass("tile-support-marker--support-exhausted");

    hover(null);

    expect(screen.queryByRole("img")).not.toBeInTheDocument();
  });

  /** The number inside is the forecast turns, regrowth included. */
  it("shows the number of turns inside the circle", () => {
    const { hover } = renderMarker();

    hover({ x: 8, y: 1 });

    expect(screen.getByRole("img", { name: "Can support for 4 turns" })).toHaveTextContent(/^4$/);
  });

  /** A tile that still feeds the forces after the forecast's last turn shows 5+ rather than a count it never worked out. */
  it("shows 5+ for a tile that outlasts the forecast", () => {
    const { hover } = renderMarker();

    hover({ x: 6, y: 1 });

    expect(screen.getByRole("img", { name: "Can support for more than 5 turns" })).toHaveTextContent(/^5\+$/);
  });

  /** No whole turn shows 0 on both dark levels; the colour and the label tell "not enough" from "nothing left". */
  it("shows 0 on an insufficient and on an exhausted tile", () => {
    const { hover } = renderMarker();

    hover({ x: 7, y: 1 });

    expect(screen.getByRole("img", { name: "Cannot support the whole force" })).toHaveTextContent(/^0$/);

    hover({ x: 3, y: 1 });

    expect(screen.getByRole("img", { name: "No food left" })).toHaveTextContent(/^0$/);
  });

  /** The tile toolbar already describes the tile the selection stands on, so the marker stays off it. */
  it("draws nothing on the selected forces' own tile", () => {
    const { hover } = renderMarker();

    hover({ x: 1, y: 1 });

    expect(screen.queryByRole("img")).not.toBeInTheDocument();
  });

  /** A level discloses what is left on a tile, so none is drawn out of sight — even if a stale answer still holds one from before the tile was lost. */
  it("draws nothing on a tile out of sight", () => {
    const { hover } = renderMarker();

    hover({ x: 5, y: 1 });

    expect(screen.queryByRole("img")).not.toBeInTheDocument();
  });

  it("draws nothing on a tile with no level", () => {
    const { hover } = renderMarker();

    hover({ x: 4, y: 1 });

    expect(screen.queryByRole("img")).not.toBeInTheDocument();
  });

  it("draws nothing before the levels have arrived", () => {
    const { hover } = renderMarker({ supportByTileKey: undefined });

    hover({ x: 2, y: 1 });

    expect(screen.queryByRole("img")).not.toBeInTheDocument();
  });
});

describe("formatSupportTurns", () => {
  it.each([
    [0, "0"],
    [1, "1"],
    [5, "5"],
    [null, "5+"],
    [undefined, ""],
  ])("formats %s as %j", (turns, expected) => {
    expect(formatSupportTurns(turns)).toBe(expected);
  });
});

describe("supportTurnsLabel", () => {
  /** With no turns said at all, the level's own wording stands in, so the marker still reads sensibly. */
  it("falls back to the level's wording when the turns are unknown", () => {
    expect(supportTurnsLabel("Plenty", undefined)).toBe("Can support for 2+ turns");
  });
});
