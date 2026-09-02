import type { MoveDirection } from "@/features/game/api/forceMovementApi";

/** The (x, y) offset one step in each direction, keyed as `"dx,dy"`. Mirrors `MapDirections` on the server. */
const DIRECTION_BY_DELTA: Readonly<Record<string, MoveDirection>> = {
  "0,-1": "North",
  "1,-1": "NorthEast",
  "1,0": "East",
  "1,1": "SouthEast",
  "0,1": "South",
  "-1,1": "SouthWest",
  "-1,0": "West",
  "-1,-1": "NorthWest",
};

/**
 * Works out which single step takes you from one tile to another, or null when the two are not neighbours.
 *
 * Null covers both "too far" and "same tile", which the caller has to tell apart from context — holding the tile a force is
 * already standing on is not a move, and neither is holding one across the map.
 *
 * Coordinates are map coordinates: X grows east, Y grows south, so North is decreasing Y.
 *
 * Wrapping is deliberately not handled. On a map whose edges join, the tile "east" of the last column is column 0, but the
 * client is not told whether a map wraps — only its width and height — so a hold across the seam reads as too far rather than
 * being guessed at. The numpad path is unaffected: it sends a direction and the server does the wrap arithmetic itself.
 */
export function directionBetween(
  origin: { x: number; y: number },
  target: { x: number; y: number },
): MoveDirection | null {
  return DIRECTION_BY_DELTA[`${target.x - origin.x},${target.y - origin.y}`] ?? null;
}
