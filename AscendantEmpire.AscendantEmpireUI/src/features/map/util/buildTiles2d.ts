import type { CampaignMapTileDto } from "@/features/map/api/playerCampaignMapApi";
import type { TileCell } from "@/features/map/components/TileTypes";

/** What one cell was last built from, so an unchanged tile can be recognised and its cell reused. */
export type TileCellSource = { source: CampaignMapTileDto; cell: TileCell };

/** The DTO-to-cell pairs from one build, to be handed back to the next. Keyed by `"x,y"`. */
export type TileCellSourceMap = Map<string, TileCellSource>;

/**
 * Builds the row-major grid the tile renderer draws from, reusing cells whose source DTO has not changed.
 *
 * `previousSourceByKey` carries the DTO each cell was last built from. A patched tiles cache replaces only the tiles that
 * actually changed, so every other DTO arrives with the same object identity and its cell can be handed back untouched —
 * which is what keeps `React.memo(Tile)` from re-rendering the whole map after a one-tile move. Building fresh cells every
 * time, as this used to, defeated that memo completely.
 *
 * Tiles outside the declared map bounds are skipped rather than growing the grid: the width and height come from the game,
 * and a tile beyond them is bad data rather than a bigger map.
 */
export function buildTiles2d(
  width: number,
  height: number,
  tiles: CampaignMapTileDto[],
  previousSourceByKey?: TileCellSourceMap,
): { grid: TileCell[][]; sourceByKey: TileCellSourceMap } {
  const grid: TileCell[][] = Array.from({ length: height }, (_, y) =>
    Array.from({ length: width }, (_, x) => ({
      x,
      y,
      biomeTypeId: null,
      geographyTypeId: null,
      // A cell the response said nothing about is ground the player has never been told about, which is unexplored by
      // definition. The grid is built at the game's full size, so most of these exist before the first tile ever arrives.
      isExplored: false,
      structures: undefined,
      forces: undefined,
    })),
  );

  const sourceByKey: TileCellSourceMap = new Map();

  for (const t of tiles) {
    if (t.xCoord < 0 || t.xCoord >= width) continue;
    if (t.yCoord < 0 || t.yCoord >= height) continue;

    const key = `${t.xCoord},${t.yCoord}`;
    const previous = previousSourceByKey?.get(key);

    // Reference equality on the DTO, not a deep comparison: the cache merge replaces the objects it changes and leaves the
    // rest alone, so identity is exactly the signal for "nothing happened to this tile".
    const cell: TileCell =
      previous?.source === t
        ? previous.cell
        : {
            x: t.xCoord,
            y: t.yCoord,
            biomeTypeId: t.biomeTypeId,
            geographyTypeId: t.campaignMapGeographyTypeId,
            isExplored: t.isExplored,
            structures: t.structures,
            forces: t.forces,
          };

    grid[t.yCoord][t.xCoord] = cell;
    sourceByKey.set(key, { source: t, cell });
  }

  return { grid, sourceByKey };
}
