import { RIVER_EDGES, type RiverEdge, type RiverWidth } from "@/types/map/Rivers";

/**
 * One of a tile's four edges as the author sees it, before it is translated to the tile that owns it.
 *
 * `side` is which edge of tile `(x, y)` was picked, so the same line on screen has two names — a tile's north edge is the tile
 * above's south edge. Which of the two the server accepts is what {@link toCanonicalEdge} works out.
 */
export type TileEdgeRef = {
  x: number;
  y: number;
  side: RiverEdge;
};

/** Which of its two edges a tile owns, spelled as the API spells it. */
export type OwnedRiverEdgeSide = "South" | "East";

/** An edge named the way the server stores it: the tile that owns it, and which of that tile's two edges it is. */
export type CanonicalRiverEdge = {
  x: number;
  y: number;
  edge: OwnedRiverEdgeSide;
};

/** Everything about the map's shape that picking an edge depends on. */
export type MapEdgeGeometry = {
  width: number;
  height: number;
  horizontalWrap: boolean;
  verticalWrap: boolean;
};

/**
 * The edge nearest a point on the grid, in grid pixels.
 *
 * The tile under the pointer is found first, then whichever of its four sides the pointer is closest to — so the picked edge
 * changes as the pointer crosses the diagonals of a tile, and every point in the grid resolves to something. Ties break in
 * {@link RIVER_EDGES} order, which only matters at the exact centre of a tile.
 *
 * Returns null for a point outside the grid, which is what a pointer leaving the layer produces.
 */
export function resolveEdgeFromPoint(offsetX: number, offsetY: number, tileSizePx: number, width: number, height: number): TileEdgeRef | null {
  if (offsetX < 0 || offsetY < 0 || offsetX >= width * tileSizePx || offsetY >= height * tileSizePx) {
    return null;
  }

  const x = Math.floor(offsetX / tileSizePx);
  const y = Math.floor(offsetY / tileSizePx);

  const withinX = offsetX - x * tileSizePx;
  const withinY = offsetY - y * tileSizePx;

  const distances: Record<RiverEdge, number> = {
    north: withinY,
    east: tileSizePx - withinX,
    south: tileSizePx - withinY,
    west: withinX,
  };

  let side: RiverEdge = "north";

  for (const candidate of RIVER_EDGES) {
    if (distances[candidate] < distances[side]) {
      side = candidate;
    }
  }

  return { x, y, side };
}

/**
 * Names an edge the way the server stores it, or null when it is not an edge at all.
 *
 * A tile owns only its South and East edges, so a north click becomes the south edge of the tile above and a west click the east
 * edge of the tile to the left. On an axis that wraps, "above" row 0 is the last row and "left of" column 0 is the last column, so
 * the seam is a real edge that can carry a river. On an axis that does not, those edges face off the map and there is nothing on
 * the other side of them — null, and the caller says so rather than sending a request the server would refuse.
 */
export function toCanonicalEdge(edge: TileEdgeRef, geometry: MapEdgeGeometry): CanonicalRiverEdge | null {
  const { x, y, side } = edge;
  const { width, height, horizontalWrap, verticalWrap } = geometry;

  if (x < 0 || y < 0 || x >= width || y >= height) {
    return null;
  }

  switch (side) {
    case "south":
      // The bottom row's south edge is shared with row 0 only where the map wraps.
      return y === height - 1 && !verticalWrap ? null : { x, y, edge: "South" };

    case "east":
      return x === width - 1 && !horizontalWrap ? null : { x, y, edge: "East" };

    case "north":
      if (y > 0) {
        return { x, y: y - 1, edge: "South" };
      }

      return verticalWrap ? { x, y: height - 1, edge: "South" } : null;

    case "west":
      if (x > 0) {
        return { x: x - 1, y, edge: "East" };
      }

      return horizontalWrap ? { x: width - 1, y, edge: "East" } : null;
  }
}

/**
 * The width a canonical edge currently carries, read off the tile that owns it.
 *
 * `"None"` for an edge with no river, and for a tile the grid does not hold — a grid still loading has no rivers to report, and
 * treating that as "no river here" is what makes a first click draw rather than erase.
 */
export function currentWidthOf(tiles2d: { southRiver: RiverWidth; eastRiver: RiverWidth }[][] | null, edge: CanonicalRiverEdge): RiverWidth {
  const tile = tiles2d?.[edge.y]?.[edge.x];

  if (tile == null) {
    return "None";
  }

  return edge.edge === "South" ? tile.southRiver : tile.eastRiver;
}

/** The line an edge is drawn along, in grid pixels. */
export type EdgeLine = { x1: number; y1: number; x2: number; y2: number };

/**
 * Where an edge sits on the grid, so the hover highlight lands exactly on the line a click would draw.
 */
export function edgeLinePx(edge: TileEdgeRef, tileSizePx: number): EdgeLine {
  const left = edge.x * tileSizePx;
  const top = edge.y * tileSizePx;
  const right = left + tileSizePx;
  const bottom = top + tileSizePx;

  switch (edge.side) {
    case "north":
      return { x1: left, y1: top, x2: right, y2: top };
    case "south":
      return { x1: left, y1: bottom, x2: right, y2: bottom };
    case "west":
      return { x1: left, y1: top, x2: left, y2: bottom };
    case "east":
      return { x1: right, y1: top, x2: right, y2: bottom };
  }
}

/** Whether two edge references point at the same edge of the same tile, for deciding if the hover has actually moved. */
export function isSameEdge(left: TileEdgeRef | null, right: TileEdgeRef | null): boolean {
  if (left == null || right == null) {
    return left === right;
  }

  return left.x === right.x && left.y === right.y && left.side === right.side;
}
