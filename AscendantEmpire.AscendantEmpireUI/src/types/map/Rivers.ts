/**
 * How wide a river along a tile edge is — the `RiverWidth` enum's member names, since the API serialises enums as strings.
 *
 * Ordered by width on the server, where the movement rules compare the two values. Nothing on the client relies on that order,
 * so these are treated as plain names here.
 */
export type RiverWidth = "None" | "Medium" | "Large";

/** The four sides of a tile, in the order rivers are listed to the player: clockwise from the top. */
export const RIVER_EDGES = ["north", "east", "south", "west"] as const;

export type RiverEdge = (typeof RIVER_EDGES)[number];

/**
 * The river on each of a tile's four edges.
 *
 * Rivers run *between* tiles, and the server resolves which side of an edge owns it before sending anything — so a tile
 * describes all four of its own edges and both banks of a river report the edge they share. Drawing each one only once is the
 * client's job; see RiverOverlay.
 *
 * Absent (rather than all-`"None"`) on a tile the player has never explored: an unexplored tile discloses no terrain at all,
 * and "there is no river here" is a different statement from "we have not been told".
 */
export type TileRivers = Record<RiverEdge, RiverWidth>;

/** How a width is named in a sentence, for tooltips and step feedback. Lower case, because these always appear mid-sentence. */
const RIVER_WIDTH_LABELS: Record<Exclude<RiverWidth, "None">, string> = {
  Medium: "medium",
  Large: "large",
};

/** Names a river width for the player, or null when there is no river to name. */
export function riverWidthLabel(width: RiverWidth): string | null {
  return width === "None" ? null : RIVER_WIDTH_LABELS[width];
}
