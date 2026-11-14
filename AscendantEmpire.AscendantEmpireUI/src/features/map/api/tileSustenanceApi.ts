import { http } from "@/api/http";

/**
 * How well a tile can feed the forces on it, or about to move onto it, from the food available to the player. Serialised by the server as the enum member's name.
 *
 * - `Plenty`: enough for two turns or more.
 * - `OneTurn`: enough for one turn, but not two.
 * - `Insufficient`: some food, but not enough for a whole turn.
 * - `Exhausted`: nothing available at all — shown even with no forces on the tile.
 *
 * A count of turns is forecast turn by turn, with the tile regrowing at the end of each round, for the forces as they stand and nobody else feeding on the tile.
 */
export type TileSupportLevel = "Plenty" | "OneTurn" | "Insufficient" | "Exhausted";

const TILE_SUPPORT_LEVELS: readonly TileSupportLevel[] = ["Plenty", "OneTurn", "Insufficient", "Exhausted"];

/** Reads a support level off the wire, treating anything unrecognised — a level added on the server first, say — as no level rather than trusting it. */
export function parseTileSupportLevel(value: unknown): TileSupportLevel | null {
  return TILE_SUPPORT_LEVELS.includes(value as TileSupportLevel) ? (value as TileSupportLevel) : null;
}

/**
 * One thing a tile offers the player to eat, measured in the individuals it feeds for a turn.
 *
 * The parts of `individualsFed`, in the same unit, so a breakdown adds up to the total beside it. A biome resource and a MapResource patch are both entries;
 * what tells them apart is `isEstimated`.
 */
export interface TileFoodContribution {
  /** What the player calls it: a biome resource's name ("Forage"), or the MapResource type's display name ("Berries"). */
  label: string;

  individualsFed: number;

  /** Whether the figure is fuzzed, which every patch's is and no biome resource's is. An estimated figure is marked with a tilde. */
  isEstimated: boolean;
}

/**
 * The Forage, Hunt, Fish and Fodder available to the current player on one tile, and what they come to in individuals fed.
 *
 * Each of the four values is what is left of the player's *own* effective value once the tile's shared "taken" amount is removed, so two players can see
 * different amounts on the same tile (see "What a tile stores: the amount taken" in docs/design/campaign-map/depletion.md).
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

  /**
   * How many individuals everything on the tile feeds for one turn, or null when there is no diet to price it against.
   *
   * Priced against the food supply set the player's forces eat from, so a berry patch feeds fewer than the same amount of game would. A player with no force
   * left that eats gets no figure, there being nothing to measure the tile's food in.
   */
  individualsFed: number | null;

  /** Whether `individualsFed` is fuzzed, which it is as soon as any MapResource patch is part of it. */
  isTotalEstimated: boolean;

  /** What the total is made of, in the same unit, biome resources first and then the patches standing on the tile. */
  foodSources: readonly TileFoodContribution[];

  /**
   * Whether the tile can feed the player's forces already standing on it. `Exhausted` for a stripped tile even with nobody on it; otherwise null when no
   * forces are on it, or when the tile is out of sight.
   */
  supportLevel: TileSupportLevel | null;
}

/** The response as it arrives. Every field is optional on the wire, so a missing one is read as absent rather than trusted. */
type TileSustenanceWireDto = {
  isObserved?: boolean;
  forage?: number | null;
  hunt?: number | null;
  fish?: number | null;
  fodder?: number | null;
  individualsFed?: number | null;
  isTotalEstimated?: boolean;
  foodSources?: readonly Partial<TileFoodContribution>[] | null;
  supportLevel?: string | null;
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
    individualsFed: data.individualsFed ?? null,
    isTotalEstimated: data.isTotalEstimated ?? false,
    // A source missing its label or its figure is dropped rather than drawn as a blank: the total is still the server's, so one part going missing
    // shows as a breakdown that does not add up, which is honest, where "(0)" would not be.
    foodSources: (data.foodSources ?? [])
      .filter((source): source is TileFoodContribution => typeof source?.label === "string" && typeof source.individualsFed === "number")
      .map((source) => ({ label: source.label, individualsFed: source.individualsFed, isEstimated: source.isEstimated === true })),
    supportLevel: parseTileSupportLevel(data.supportLevel),
  };
}
