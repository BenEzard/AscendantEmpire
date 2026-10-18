import type { BorderStroke } from "@/components/map/BorderStrokeLayer";
import { BORDER_STROKE_PX } from "@/components/map/BorderStrokeLayer";
import type { TileCell } from "@/features/map/components/TileTypes";
import { TILE_EDGES, type TileEdge } from "@/types/map/Ownership";

/**
 * What a tile belongs to, for the purpose of drawing borders.
 *
 * A border is drawn on a side where the tile's band differs from its neighbour's, so the `key` is the whole of the comparison: two
 * tiles with the same key are the same territory and nothing is drawn between them, whatever else differs about them.
 */
export type OwnershipBand = {
  /** The identity two neighbours must share for no border to run between them. */
  key: string;
  /**
   * The colours this band's side of a border is drawn in, in drawing order.
   *
   * One for owned ground. One per claimant for claimed ground, whose colours take turns along the dashes — claims are the only
   * non-exclusive state, so a claimed tile can legitimately belong to several factions at once.
   */
  colours: readonly string[];
  /** Dashed for a claim, solid for ownership. */
  isDashed: boolean;
};

export type OwnershipBandContext = {
  /** Each player's faction colour. A player with none falls back to the neutral below, rather than going undrawn. */
  factionColourByPlayerId: ReadonlyMap<number, string>;
  /** Whether an observer can see the tile right now. Unwatched ground has no band at all — see below. */
  isTileObserved: (tile: TileCell) => boolean;
};

/** Where a player has no faction colour of their own. Named so a missing colour looks like a gap rather than like someone's territory. */
const UNKNOWN_HOLDER_COLOUR = "var(--campaign-border-unknown)";

/**
 * What territory a tile belongs to, or null when it belongs to none that is worth drawing.
 *
 * Null covers three different cases, all of which draw nothing:
 *
 * - **Nothing is watching it.** Ownership is current information and the server only reports it under live sight, so an unwatched
 *   tile has no ownership to read. Gating on sight here as well means a border never lingers from whatever the tiles cache last
 *   held for a tile the player has since looked away from.
 * - **Nobody holds it.** Wilderness, and most of a map.
 * - **It is abandoned.** Held by nobody, by definition — the former owner is remembered so the tile can revert, not so a border
 *   can be drawn around ground its owner has walked away from.
 *
 * An **occupied** tile takes its *owner's* band, not its occupier's. Occupation changes who collects the yield and never who owns
 * the tile, so the border it sits inside is still the owner's; that the tile is occupied is said in the tooltip instead.
 */
export function getOwnershipBand(tile: TileCell, context: OwnershipBandContext): OwnershipBand | null {
  if (!context.isTileObserved(tile)) {
    return null;
  }

  const ownership = tile.ownership;

  if (ownership == null) {
    return null;
  }

  const colourOf = (playerId: number) => context.factionColourByPlayerId.get(playerId) ?? UNKNOWN_HOLDER_COLOUR;

  if (ownership.ownedByPlayerId != null) {
    return {
      key: `owned:${ownership.ownedByPlayerId}`,
      colours: [colourOf(ownership.ownedByPlayerId)],
      isDashed: false,
    };
  }

  // Claims only ever describe unowned ground, because ownership extinguishes every rival claim on a tile. Reached second for
  // exactly that reason: an owned tile cannot also be claimed.
  if (ownership.claimedByPlayerIds.length > 0) {
    // Every claimant is in the key, so ground claimed by one player, by another, and by both are three different bands with
    // lines where they meet. The server's order is kept rather than re-sorted, because that order is what the tooltip lists.
    return {
      key: `claim:${ownership.claimedByPlayerIds.join("-")}`,
      colours: ownership.claimedByPlayerIds.map(colourOf),
      isDashed: true,
    };
  }

  return null;
}

/** The neighbour on each side, as an offset into the row-major grid. Diagonals do not matter: a border runs along a shared edge. */
const EDGE_OFFSETS: Record<TileEdge, { dx: number; dy: number }> = {
  north: { dx: 0, dy: -1 },
  east: { dx: 1, dy: 0 },
  south: { dx: 0, dy: 1 },
  west: { dx: -1, dy: 0 },
};

/**
 * Every border segment to draw over the grid, in grid pixels.
 *
 * A tile draws a segment on each of its own four sides where the neighbour across it belongs to a different band — including where
 * there is no neighbour at all, so a territory reaching the edge of the grid or the edge of what the player can see is still
 * outlined. Each side is therefore drawn by the tile that owns it, which is what lets a frontier show both factions' colours at
 * once: each side's stroke is inset into its own tile, so the two run side by side instead of overwriting one line.
 *
 * **A segment spans its whole tile edge.** The inset moves a line *across* the edge it describes, into its own tile, and is
 * deliberately not applied along the line's length: shortening both ends would leave a stroke-width of bare ground between the
 * borders of two stacked tiles of one territory, so a straight frontier would come out dashed. Ending each segment exactly on the
 * tile boundary means the neighbour's begins where this one stops — `strokeLinecap="butt"`, so they abut without overlapping — and
 * it fills the notch an outside corner would otherwise have.
 *
 * **A claim yields its side to owned ground across the same edge.** Without that, the boundary between a claim and someone's
 * territory draws twice: a solid line in the owner's tile and a dashed one 3px away in the claimant's. Ownership extinguishes
 * claims, so the owned line is the one worth seeing, and the claimant simply leaves that side undrawn. It costs nothing — the
 * neighbour's band is already in hand for the comparison above — and it applies only to the two lines that would run parallel
 * along one shared edge, so a claim keeps every other side it has.
 *
 * **Map wrapping is not honoured**, deliberately and in step with RiverOverlay: the grid is indexed row-major with no wrap, so a
 * territory spanning the seam of a wrapping world is outlined on both sides of the seam rather than joined across it.
 */
export function collectBorderStrokes(tiles2d: readonly TileCell[][], tileSizePx: number, context: OwnershipBandContext): BorderStroke[] {
  const strokes: BorderStroke[] = [];

  // Half the stroke, so the line sits wholly inside its own tile and the neighbour's line sits wholly inside theirs.
  const inset = BORDER_STROKE_PX / 2;

  for (const row of tiles2d) {
    for (const tile of row) {
      const band = getOwnershipBand(tile, context);

      if (band == null) {
        continue;
      }

      // The tile's own bounds, which is how far along its edge a segment runs.
      const edgeLeft = tile.x * tileSizePx;
      const edgeTop = tile.y * tileSizePx;
      const edgeRight = edgeLeft + tileSizePx;
      const edgeBottom = edgeTop + tileSizePx;

      // Where across that edge the line sits: a stroke-half inside the tile, so the neighbour's line on the same edge sits a
      // stroke-half inside theirs and the two run alongside each other rather than on top of one another.
      const lineLeft = edgeLeft + inset;
      const lineTop = edgeTop + inset;
      const lineRight = edgeRight - inset;
      const lineBottom = edgeBottom - inset;

      for (const edge of TILE_EDGES) {
        const offset = EDGE_OFFSETS[edge];
        const neighbour = tiles2d[tile.y + offset.dy]?.[tile.x + offset.dx];

        // An absent neighbour is off the grid, and a neighbour in no band is wilderness, unwatched, or someone else's business —
        // either way this tile's territory ends here and the side gets a line.
        const neighbourBand = neighbour == null ? null : getOwnershipBand(neighbour, context);

        if (neighbourBand?.key === band.key) {
          continue;
        }

        // Owned ground across this edge wins it outright: ownership extinguishes claims, so a dashed line beside the owner's
        // solid one says nothing the solid one has not already said. Only this one shared edge is given up.
        if (band.isDashed && neighbourBand != null && neighbourBand.isDashed === false) {
          continue;
        }

        const key = `${tile.x},${tile.y},${edge}`;
        const paint = { colours: band.colours, isDashed: band.isDashed };

        switch (edge) {
          case "north":
            strokes.push({ key, x1: edgeLeft, y1: lineTop, x2: edgeRight, y2: lineTop, ...paint });
            break;
          case "east":
            strokes.push({ key, x1: lineRight, y1: edgeTop, x2: lineRight, y2: edgeBottom, ...paint });
            break;
          case "south":
            strokes.push({ key, x1: edgeLeft, y1: lineBottom, x2: edgeRight, y2: lineBottom, ...paint });
            break;
          case "west":
            strokes.push({ key, x1: lineLeft, y1: edgeTop, x2: lineLeft, y2: edgeBottom, ...paint });
            break;
        }
      }
    }
  }

  return strokes;
}
