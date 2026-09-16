import { http } from "@/api/http";

/**
 * The Forage, Hunt, Fish and Fodder available to the current player on one tile.
 *
 * Each value is what is left of the player's *own* effective value once the tile's shared "taken" amount is removed, so two players can see different
 * amounts on the same tile (see "What a tile stores: the amount taken" in docs/design/campaign-map.md).
 *
 * A null value means the resource does not exist in the tile's biome, which is not the same as 0 — a tile that has it but has been stripped bare.
 */
export interface TileSustenance {
  /**
   * Whether the tile is in the player's live sight. When it is not, the server discloses nothing and every value is null: what is left on a tile changes
   * as anyone feeds on it, so reporting it on an unwatched tile would give away where other players' forces have been.
   */
  isObserved: boolean;
  forage: number | null;
  hunt: number | null;
  fish: number | null;
  fodder: number | null;
}

/** The response as it arrives. Every field is optional on the wire, so a missing one is read as absent rather than trusted. */
type TileSustenanceWireDto = {
  isObserved?: boolean;
  forage?: number | null;
  hunt?: number | null;
  fish?: number | null;
  fodder?: number | null;
};

/** What is available to the player on the tile at (x, y). The game and player come from the game token, not the request. */
export async function getTileSustenance(x: number, y: number): Promise<TileSustenance> {
  const { data } = await http.get<TileSustenanceWireDto>(`/campaign-map/player/tiles/${x}/${y}/sustenance`);

  return {
    isObserved: data.isObserved ?? false,
    forage: data.forage ?? null,
    hunt: data.hunt ?? null,
    fish: data.fish ?? null,
    fodder: data.fodder ?? null,
  };
}
