export interface TileToolbarTile {
  x: number;
  y: number;

  /**
   * The tile's terrain, already named ("Grassland, Hills", or "Unknown" for ground the player has never seen). Resolved by
   * the caller, which holds the biome and geography lookups — the bar only draws what it is given.
   */
  terrainLabel: string;
}

export interface TileToolbarProps {
  /** The tile the player last clicked, or null when there is nothing to describe (renders nothing). */
  tile: TileToolbarTile | null;
}

/**
 * Describes the tile the player last clicked: its coordinates and terrain. Docked to the left of the force selection bar,
 * replacing the toast a tile click used to raise — a toast vanished before the player had finished reasoning about the tile,
 * where this stays up until another tile is clicked.
 *
 * A presenter, like its neighbours in the dock: it holds no state and makes no decisions.
 */
export default function TileToolbar({ tile }: TileToolbarProps) {
  if (tile == null) {
    return null;
  }

  return (
    <div className="tile-toolbar" aria-label="Tile toolbar">
      <span className="tile-toolbar__coordinates">
        ({tile.x}, {tile.y})
      </span>
      <span className="tile-toolbar__terrain">{tile.terrainLabel}</span>
    </div>
  );
}
