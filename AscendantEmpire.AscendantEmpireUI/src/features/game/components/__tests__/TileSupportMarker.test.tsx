import { act, render, screen } from "@testing-library/react";
import TileSupportMarker, { type TileSupportMarkerProps } from "../TileSupportMarker";
import { createHoveredTileStore } from "@/features/map/hooks/useHoveredTileStore";
import type { TileSupportLevel } from "@/features/map/api/tileSustenanceApi";

// The selected force stands on (1, 1). (2, 1) and (3, 1) are in sight with a level; (4, 1) is in sight with none; (5, 1) is out of sight, though a stale
// answer still holds a level for it.
const LEVELS = new Map<string, TileSupportLevel>([
  ["1,1", "Plenty"],
  ["2,1", "OneTurn"],
  ["3,1", "Exhausted"],
  ["5,1", "Insufficient"],
]);

const OBSERVED = new Set(["1,1", "2,1", "3,1", "4,1"]);
const SELECTED_FORCE_TILES = new Set(["1,1"]);

function renderMarker(props?: Partial<TileSupportMarkerProps>) {
  const hoveredTileStore = createHoveredTileStore();

  render(
    <TileSupportMarker
      hoveredTileStore={hoveredTileStore}
      levelByTileKey={LEVELS}
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
    const { hover } = renderMarker({ levelByTileKey: undefined });

    hover({ x: 2, y: 1 });

    expect(screen.queryByRole("img")).not.toBeInTheDocument();
  });
});
