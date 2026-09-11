import type { MoveDirection } from "@/features/game/api/forceMovementApi";
import type { KnownForce } from "./api/intelligenceApi";

/** How opaque a marker is in the round its force was seen. Short of fully opaque, so a memory never reads as a live sighting. */
export const KNOWN_FORCE_FRESH_OPACITY = 0.85;

/** The faintest a marker ever gets. It fades to here and stays: whether markers should ever retire is an open design question, so nothing is hidden by age. */
export const KNOWN_FORCE_STALE_OPACITY = 0.3;

/** How many rounds a marker takes to fade from fresh to stale. */
export const KNOWN_FORCE_FADE_ROUNDS = 5;

/**
 * How opaque a last-known-position marker should be, given how long ago its force was seen.
 *
 * Linear from fresh to stale over `KNOWN_FORCE_FADE_ROUNDS`. When the current round is not known — nobody is taking a turn, or the turn read failed — the
 * marker is drawn fresh rather than guessed at: showing a memory too clearly is harmless, fading one on no evidence is not.
 */
export function knownForceOpacity(lastSeenRoundNumber: number, currentRoundNumber: number | null | undefined): number {
  if (currentRoundNumber == null) {
    return KNOWN_FORCE_FRESH_OPACITY;
  }

  // Never negative: a round number from the future is a stale cache, not a memory fresher than fresh.
  const roundsAgo = Math.max(0, currentRoundNumber - lastSeenRoundNumber);

  // The floor exactly, rather than the interpolation's floating-point approximation of it, so every fully faded marker is drawn identically.
  if (roundsAgo >= KNOWN_FORCE_FADE_ROUNDS) {
    return KNOWN_FORCE_STALE_OPACITY;
  }

  const fadedFraction = roundsAgo / KNOWN_FORCE_FADE_ROUNDS;

  return KNOWN_FORCE_FRESH_OPACITY - (KNOWN_FORCE_FRESH_OPACITY - KNOWN_FORCE_STALE_OPACITY) * fadedFraction;
}

/** Degrees to rotate an east-pointing arrow so it points the given way. Screen rotation is clockwise and Y grows south, so South is a quarter turn. */
const HEADING_DEGREES: Readonly<Record<MoveDirection, number>> = {
  East: 0,
  SouthEast: 45,
  South: 90,
  SouthWest: 135,
  West: 180,
  NorthWest: 225,
  North: 270,
  NorthEast: 315,
};

export function headingDegrees(direction: MoveDirection): number {
  return HEADING_DEGREES[direction];
}

/**
 * Groups known forces by the tile they were last seen on, leaving out every tile the player can see right now.
 *
 * A marker is a memory, and on a tile under live sight the map already shows what is really there — a marker beside it would be at best redundant and at worst
 * a force drawn where the player can plainly see there is none. Within a tile the freshest sighting comes first, which is the one the marker shows.
 *
 * @param observedTileKeys The `"x,y"` key of every tile currently in sight, as `useFogOfWar` derives it.
 */
export function groupKnownForcesByUnobservedTile(knownForces: readonly KnownForce[], observedTileKeys: ReadonlySet<string>): ReadonlyMap<string, KnownForce[]> {
  const byTile = new Map<string, KnownForce[]>();

  for (const knownForce of knownForces) {
    const key = `${knownForce.lastSeenLocation.x},${knownForce.lastSeenLocation.y}`;

    if (observedTileKeys.has(key)) {
      continue;
    }

    const onTile = byTile.get(key);

    if (onTile) {
      onTile.push(knownForce);
    } else {
      byTile.set(key, [knownForce]);
    }
  }

  // Turn ids only ever increase, so (turn, sequence) orders sightings the way the server does.
  for (const onTile of byTile.values()) {
    onTile.sort((a, b) => b.lastSeenPlayerTurnId - a.lastSeenPlayerTurnId || b.lastSeenTurnSequence - a.lastSeenTurnSequence);
  }

  return byTile;
}
