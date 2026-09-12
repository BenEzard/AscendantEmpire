import type { KnownStructure } from "./api/intelligenceApi";

/**
 * Groups known structures by their tile, leaving out every tile the player can see right now.
 *
 * The structure counterpart of `groupKnownForcesByUnobservedTile`, for the same reason: under live sight the map already shows what really stands on a tile, so a
 * marker there would at best repeat it and at worst draw a structure the player can see has gone. Within a tile the freshest sighting comes first, which is the
 * one the marker shows. Markers fade with `knownForceOpacity`, so both kinds of memory age alike.
 *
 * @param observedTileKeys The `"x,y"` key of every tile currently in sight, as `useFogOfWar` derives it.
 */
export function groupKnownStructuresByUnobservedTile(knownStructures: readonly KnownStructure[], observedTileKeys: ReadonlySet<string>): ReadonlyMap<string, KnownStructure[]> {
  const byTile = new Map<string, KnownStructure[]>();

  for (const knownStructure of knownStructures) {
    const key = `${knownStructure.location.x},${knownStructure.location.y}`;

    if (observedTileKeys.has(key)) {
      continue;
    }

    const onTile = byTile.get(key);

    if (onTile) {
      onTile.push(knownStructure);
    } else {
      byTile.set(key, [knownStructure]);
    }
  }

  // Turn ids only ever increase, so (turn, sequence) orders sightings the way the server does.
  for (const onTile of byTile.values()) {
    onTile.sort((a, b) => b.lastSeenPlayerTurnId - a.lastSeenPlayerTurnId || b.lastSeenTurnSequence - a.lastSeenTurnSequence);
  }

  return byTile;
}
