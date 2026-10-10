import { buildTiles2d, type TileCellSourceMap } from "@/features/map/util/buildTiles2d";
import type { CampaignMapTileDto } from "@/features/map/api/playerCampaignMapApi";

function tile(x: number, y: number, isExplored = true): CampaignMapTileDto {
  // Biome and Geography arrive as their enum member names, not numbers — see BiomeType in @/types/map/Biome.
  return { xCoord: x, yCoord: y, biomeTypeId: "Coastal", campaignMapGeographyTypeId: "Flat", isExplored, structures: [], forces: [] };
}

describe("buildTiles2d", () => {
  it("places each tile at its coordinate in a row-major grid", () => {
    const { grid } = buildTiles2d(3, 2, [tile(2, 1)]);

    expect(grid).toHaveLength(2);
    expect(grid[0]).toHaveLength(3);
    expect(grid[1][2].biomeTypeId).toBe("Coastal");
    expect(grid[0][0].biomeTypeId).toBeNull();
  });

  it("ignores tiles outside the declared map bounds rather than growing the grid", () => {
    const { grid } = buildTiles2d(2, 2, [tile(5, 0), tile(0, 9)]);

    expect(grid.every((row) => row.every((cell) => cell.biomeTypeId === null))).toBe(true);
  });

  it("hands back the same cell object when a tile's source is unchanged", () => {
    // This is what keeps React.memo(Tile) working: the cache patch replaces only the tiles that changed, so every
    // other DTO arrives with the same identity and its cell must not be rebuilt.
    const unchanged = tile(0, 0);
    const first = buildTiles2d(2, 1, [unchanged, tile(1, 0)]);

    const second = buildTiles2d(2, 1, [unchanged, tile(1, 0)], first.sourceByKey satisfies TileCellSourceMap);

    expect(second.grid[0][0]).toBe(first.grid[0][0]);
  });

  it("rebuilds only the cell whose source changed", () => {
    const unchanged = tile(0, 0);
    const first = buildTiles2d(2, 1, [unchanged, tile(1, 0)]);

    // A fresh object for (1, 0) stands for the tile the merge actually replaced.
    const second = buildTiles2d(2, 1, [unchanged, tile(1, 0)], first.sourceByKey satisfies TileCellSourceMap);

    expect(second.grid[0][0]).toBe(first.grid[0][0]);
    expect(second.grid[0][1]).not.toBe(first.grid[0][1]);
  });

  it("builds every cell fresh when there is no previous grid to reuse", () => {
    const shared = tile(0, 0);
    const first = buildTiles2d(1, 1, [shared]);
    const second = buildTiles2d(1, 1, [shared]);

    expect(second.grid[0][0]).not.toBe(first.grid[0][0]);
  });
});
