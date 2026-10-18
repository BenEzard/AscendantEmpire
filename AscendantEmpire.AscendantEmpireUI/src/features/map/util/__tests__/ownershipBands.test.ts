import type { TileCell } from "@/features/map/components/TileTypes";
import type { TileOwnership } from "@/types/map/Ownership";
import { collectBorderStrokes, getOwnershipBand, type OwnershipBandContext } from "../ownershipBands";

const ROME = 1;
const CARTHAGE = 2;

const ROME_COLOUR = "#b91c1c";
const CARTHAGE_COLOUR = "#1d4ed8";

const FACTION_COLOURS = new Map([
  [ROME, ROME_COLOUR],
  [CARTHAGE, CARTHAGE_COLOUR],
]);

/** The ownership record a watched tile carries, with the layers a test cares about overridden. */
function ownership(overrides: Partial<TileOwnership>): TileOwnership {
  return {
    status: "Unclaimed",
    ownedByPlayerId: null,
    occupiedByPlayerId: null,
    lastOwnedByPlayerId: null,
    effectiveHolderPlayerId: null,
    claimedByPlayerIds: [],
    isRebelling: false,
    ...overrides,
  };
}

function tile(x: number, y: number, tileOwnership?: TileOwnership): TileCell {
  return { x, y, biomeTypeId: "Grassland", geographyTypeId: "Flat", isExplored: true, ownership: tileOwnership };
}

/** Everything watched, which is what most of these tests want: the sight rule gets a section of its own below. */
function context(overrides: Partial<OwnershipBandContext> = {}): OwnershipBandContext {
  return {
    factionColourByPlayerId: FACTION_COLOURS,
    isTileObserved: () => true,
    ...overrides,
  };
}

/**
 * A band is the identity two neighbours must share for no border to run between them, so what it is made of decides where lines
 * appear. These pin the precedence between the layers and which cases are deliberately drawn as nothing at all.
 */
describe("getOwnershipBand", () => {
  it("puts an owned tile in its owner's band, in their faction colour and drawn solid", () => {
    const band = getOwnershipBand(tile(0, 0, ownership({ status: "Owned", ownedByPlayerId: ROME, effectiveHolderPlayerId: ROME })), context());

    expect(band).toEqual({ key: `owned:${ROME}`, colours: [ROME_COLOUR], isDashed: false });
  });

  /**
   * Occupation changes who collects a tile's yield and never who owns it, so the border the tile sits inside is still its
   * owner's. Banding it with the occupier would redraw a besieged player's frontier as the besieger's territory.
   */
  it("keeps an occupied tile in its owner's band rather than its occupier's", () => {
    const band = getOwnershipBand(
      tile(0, 0, ownership({ status: "Occupied", ownedByPlayerId: ROME, occupiedByPlayerId: CARTHAGE, effectiveHolderPlayerId: CARTHAGE })),
      context(),
    );

    expect(band?.key).toBe(`owned:${ROME}`);
    expect(band?.colours).toEqual([ROME_COLOUR]);
  });

  /** A claim is an assertion rather than control, and the dashed line is what says which of the two the player is looking at. */
  it("draws a single claimant's claim dashed, in that claimant's own colour", () => {
    const band = getOwnershipBand(tile(0, 0, ownership({ status: "Claimed", claimedByPlayerIds: [CARTHAGE] })), context());

    expect(band).toEqual({ key: `claim:${CARTHAGE}`, colours: [CARTHAGE_COLOUR], isDashed: true });
  });

  /**
   * Claims are not exclusive, so a tile several players assert carries all their colours at once — the stroke layer alternates
   * them along the dashes. The order is the server's, kept as sent so it matches the tooltip's list.
   */
  it("carries every claimant's colour, in the order the server sent them", () => {
    const band = getOwnershipBand(tile(0, 0, ownership({ status: "Claimed", claimedByPlayerIds: [ROME, CARTHAGE] })), context());

    expect(band?.colours).toEqual([ROME_COLOUR, CARTHAGE_COLOUR]);
    expect(band?.isDashed).toBe(true);
  });

  /** Ground claimed by one player, by another, and by both are three different territories, so lines run where they meet. */
  it("gives a tile claimed by two players a band of its own, distinct from either claiming it alone", () => {
    const rome = getOwnershipBand(tile(0, 0, ownership({ status: "Claimed", claimedByPlayerIds: [ROME] })), context());
    const both = getOwnershipBand(tile(1, 0, ownership({ status: "Claimed", claimedByPlayerIds: [ROME, CARTHAGE] })), context());
    const carthage = getOwnershipBand(tile(2, 0, ownership({ status: "Claimed", claimedByPlayerIds: [CARTHAGE] })), context());

    expect(both?.key).not.toBe(rome?.key);
    expect(both?.key).not.toBe(carthage?.key);
  });

  /** Wilderness is most of a map, and outlining every tile of it would bury the borders that mean something. */
  it("puts an unclaimed tile in no band", () => {
    expect(getOwnershipBand(tile(0, 0, ownership({ status: "Unclaimed" })), context())).toBeNull();
  });

  /**
   * An abandoned tile is held by nobody. The former owner is remembered so the tile can revert to wilderness and so the tooltip
   * can say whose it was — not so a border can be drawn around ground its owner has walked away from.
   */
  it("puts an abandoned tile in no band, though it remembers its former owner", () => {
    const band = getOwnershipBand(tile(0, 0, ownership({ status: "Abandoned", lastOwnedByPlayerId: ROME })), context());

    expect(band).toBeNull();
  });

  /** A holder with no faction colour still gets a border, in the neutral — a missing colour is a gap, not an invisible empire. */
  it("falls back to a neutral colour for a holder with no faction colour set", () => {
    const band = getOwnershipBand(tile(0, 0, ownership({ status: "Owned", ownedByPlayerId: 99, effectiveHolderPlayerId: 99 })), context());

    expect(band).not.toBeNull();
    expect(band?.colours[0]).not.toBe(ROME_COLOUR);
  });

  /** The same fallback per claimant, so one unknown claimant does not cost the others their colours. */
  it("falls back per claimant, keeping the known claimants' colours", () => {
    const band = getOwnershipBand(tile(0, 0, ownership({ status: "Claimed", claimedByPlayerIds: [ROME, 99] })), context());

    expect(band?.colours).toHaveLength(2);
    expect(band?.colours[0]).toBe(ROME_COLOUR);
    expect(band?.colours[1]).not.toBe(ROME_COLOUR);
  });
});

/**
 * Ownership is current information, so the API reports it under live sight only. The overlay gates on sight again before drawing,
 * because the tiles cache keeps the last ownership it was told about for a tile the player has since looked away from.
 */
describe("getOwnershipBand under fog", () => {
  it("puts an unwatched tile in no band even while the cache still holds its last known owner", () => {
    const owned = tile(0, 0, ownership({ status: "Owned", ownedByPlayerId: ROME, effectiveHolderPlayerId: ROME }));

    const watched = getOwnershipBand(owned, context({ isTileObserved: () => true }));
    const unwatched = getOwnershipBand(owned, context({ isTileObserved: () => false }));

    expect(watched).not.toBeNull();
    expect(unwatched).toBeNull();
  });

  it("puts a tile the API reported no ownership for in no band", () => {
    expect(getOwnershipBand(tile(0, 0, undefined), context())).toBeNull();
  });
});

/**
 * A tile draws a line on each of its own sides where the neighbour across it belongs to a different band. Each side is drawn by
 * the tile that owns it, inset into that tile, which is what lets a frontier show both factions' colours side by side.
 */
describe("collectBorderStrokes", () => {
  const TILE_SIZE_PX = 50;

  /** The sides a given tile drew, by name, read back off the keys the collector emits. */
  function sidesDrawnAt(strokes: readonly { key: string }[], x: number, y: number): string[] {
    return strokes.filter((stroke) => stroke.key.startsWith(`${x},${y},`)).map((stroke) => stroke.key.split(",")[2]);
  }

  /** One owned tile alone on a 3 x 3 of wilderness: every side of it is a frontier, including the sides facing nothing. */
  it("outlines a lone owned tile on all four sides", () => {
    const owned = ownership({ status: "Owned", ownedByPlayerId: ROME, effectiveHolderPlayerId: ROME });
    const tiles2d = [
      [tile(0, 0), tile(1, 0), tile(2, 0)],
      [tile(0, 1), tile(1, 1, owned), tile(2, 1)],
      [tile(0, 2), tile(1, 2), tile(2, 2)],
    ];

    const strokes = collectBorderStrokes(tiles2d, TILE_SIZE_PX, context());

    expect(strokes).toHaveLength(4);
    expect(sidesDrawnAt(strokes, 1, 1).sort()).toEqual(["east", "north", "south", "west"]);
    expect(strokes.every((stroke) => stroke.colours[0] === ROME_COLOUR)).toBe(true);
  });

  /** The interior of a territory carries no lines, which is the whole point of banding tiles rather than outlining each one. */
  it("draws nothing between two tiles of the same band", () => {
    const owned = ownership({ status: "Owned", ownedByPlayerId: ROME, effectiveHolderPlayerId: ROME });
    const tiles2d = [[tile(0, 0, owned), tile(1, 0, owned)]];

    const strokes = collectBorderStrokes(tiles2d, TILE_SIZE_PX, context());

    expect(sidesDrawnAt(strokes, 0, 0)).not.toContain("east");
    expect(sidesDrawnAt(strokes, 1, 0)).not.toContain("west");
  });

  /** Two tiles claimed by the same set of players are the same territory, so no line runs between them either. */
  it("draws nothing between two tiles claimed by the same players", () => {
    const claimed = ownership({ status: "Claimed", claimedByPlayerIds: [ROME, CARTHAGE] });
    const tiles2d = [[tile(0, 0, claimed), tile(1, 0, claimed)]];

    const strokes = collectBorderStrokes(tiles2d, TILE_SIZE_PX, context());

    expect(sidesDrawnAt(strokes, 0, 0)).not.toContain("east");
  });

  /** A frontier between two factions is two lines, one each, so it reads as two colours meeting rather than as one arbitrary side. */
  it("draws both sides of a frontier, each in its own faction's colour", () => {
    const romeOwned = ownership({ status: "Owned", ownedByPlayerId: ROME, effectiveHolderPlayerId: ROME });
    const carthageOwned = ownership({ status: "Owned", ownedByPlayerId: CARTHAGE, effectiveHolderPlayerId: CARTHAGE });
    const tiles2d = [[tile(0, 0, romeOwned), tile(1, 0, carthageOwned)]];

    const strokes = collectBorderStrokes(tiles2d, TILE_SIZE_PX, context());

    const romeEast = strokes.find((stroke) => stroke.key === "0,0,east");
    const carthageWest = strokes.find((stroke) => stroke.key === "1,0,west");

    expect(romeEast?.colours).toEqual([ROME_COLOUR]);
    expect(carthageWest?.colours).toEqual([CARTHAGE_COLOUR]);
  });

  /**
   * A straight frontier has to come out as one unbroken line. The inset moves a line across the edge it describes, into its own
   * tile; applying it along the line's length as well would clip a stroke-width off each end, so two stacked tiles of one
   * territory would leave bare ground between their borders and a straight edge would read as dashed.
   */
  it("runs each segment the whole length of its tile edge, so stacked tiles' borders meet", () => {
    const owned = ownership({ status: "Owned", ownedByPlayerId: ROME, effectiveHolderPlayerId: ROME });
    const tiles2d = [[tile(0, 0, owned)], [tile(0, 1, owned)]];

    const strokes = collectBorderStrokes(tiles2d, TILE_SIZE_PX, context());

    const upper = strokes.find((stroke) => stroke.key === "0,0,west")!;
    const lower = strokes.find((stroke) => stroke.key === "0,1,west")!;

    // The upper segment ends exactly where the lower one begins - no gap, and with butt caps no overlap either.
    expect(upper.y2).toBe(lower.y1);
    expect(upper.y1).toBe(0);
    expect(lower.y2).toBe(2 * TILE_SIZE_PX);
  });

  /** The same for a horizontal run, and it is what fills the notch an outside corner would otherwise have. */
  it("runs a horizontal segment corner to corner of its tile", () => {
    const owned = ownership({ status: "Owned", ownedByPlayerId: ROME, effectiveHolderPlayerId: ROME });
    const tiles2d = [[tile(0, 0, owned), tile(1, 0, owned)]];

    const strokes = collectBorderStrokes(tiles2d, TILE_SIZE_PX, context());

    const left = strokes.find((stroke) => stroke.key === "0,0,north")!;
    const right = strokes.find((stroke) => stroke.key === "1,0,north")!;

    expect(left.x1).toBe(0);
    expect(left.x2).toBe(right.x1);
    expect(right.x2).toBe(2 * TILE_SIZE_PX);
  });

  /** Each side's line is inset into its own tile, so the two sides of a frontier sit beside each other instead of on one line. */
  it("insets each side's line inside its own tile", () => {
    const romeOwned = ownership({ status: "Owned", ownedByPlayerId: ROME, effectiveHolderPlayerId: ROME });
    const carthageOwned = ownership({ status: "Owned", ownedByPlayerId: CARTHAGE, effectiveHolderPlayerId: CARTHAGE });
    const tiles2d = [[tile(0, 0, romeOwned), tile(1, 0, carthageOwned)]];

    const strokes = collectBorderStrokes(tiles2d, TILE_SIZE_PX, context());

    const romeEast = strokes.find((stroke) => stroke.key === "0,0,east")!;
    const carthageWest = strokes.find((stroke) => stroke.key === "1,0,west")!;

    // The shared edge lies at x = 50. Rome's line is left of it and Carthage's right of it, so neither covers the other.
    expect(romeEast.x1).toBeLessThan(TILE_SIZE_PX);
    expect(carthageWest.x1).toBeGreaterThan(TILE_SIZE_PX);
  });

  /**
   * Ownership extinguishes claims, so the owned line wins the edge the two share outright. Drawing both would put a dashed line
   * 3px from a solid one saying nothing the solid one has not already said.
   */
  it("gives the shared edge to owned ground and leaves the claim's facing side undrawn", () => {
    const owned = ownership({ status: "Owned", ownedByPlayerId: ROME, effectiveHolderPlayerId: ROME });
    const claimed = ownership({ status: "Claimed", claimedByPlayerIds: [ROME] });
    const tiles2d = [[tile(0, 0, owned), tile(1, 0, claimed)]];

    const strokes = collectBorderStrokes(tiles2d, TILE_SIZE_PX, context());

    expect(strokes.find((stroke) => stroke.key === "0,0,east")?.isDashed).toBe(false);
    expect(strokes.find((stroke) => stroke.key === "1,0,west")).toBeUndefined();
  });

  /** Only the one shared edge is given up. A claim bordering owned ground on one side keeps every other side it has. */
  it("keeps a claim's other sides when it yields one edge to owned ground", () => {
    const owned = ownership({ status: "Owned", ownedByPlayerId: ROME, effectiveHolderPlayerId: ROME });
    const claimed = ownership({ status: "Claimed", claimedByPlayerIds: [ROME] });
    const tiles2d = [[tile(0, 0, owned), tile(1, 0, claimed), tile(2, 0)]];

    const strokes = collectBorderStrokes(tiles2d, TILE_SIZE_PX, context());

    expect(sidesDrawnAt(strokes, 1, 0).sort()).toEqual(["east", "north", "south"]);
  });

  /** The rule is about ownership beating a claim, not about dashes losing to solids in general: claim meets claim is unchanged. */
  it("draws both sides where two different claims meet", () => {
    const mine = ownership({ status: "Claimed", claimedByPlayerIds: [ROME] });
    const theirs = ownership({ status: "Claimed", claimedByPlayerIds: [CARTHAGE] });
    const tiles2d = [[tile(0, 0, mine), tile(1, 0, theirs)]];

    const strokes = collectBorderStrokes(tiles2d, TILE_SIZE_PX, context());

    expect(strokes.find((stroke) => stroke.key === "0,0,east")).toBeDefined();
    expect(strokes.find((stroke) => stroke.key === "1,0,west")).toBeDefined();
  });

  /**
   * The fog edge is a border too: the player sees where their sight of a territory ends, rather than a line that runs on into
   * ground nothing is watching.
   */
  it("outlines a territory against ground nothing is watching", () => {
    const owned = ownership({ status: "Owned", ownedByPlayerId: ROME, effectiveHolderPlayerId: ROME });
    const tiles2d = [[tile(0, 0, owned), tile(1, 0, owned)]];

    // Only the left tile is watched, so the right one drops out of its band and the line falls between them.
    const strokes = collectBorderStrokes(tiles2d, TILE_SIZE_PX, context({ isTileObserved: (t) => t.x === 0 }));

    expect(sidesDrawnAt(strokes, 0, 0)).toContain("east");
    expect(sidesDrawnAt(strokes, 1, 0)).toHaveLength(0);
  });

  it("draws nothing at all over a map nobody holds any of", () => {
    const tiles2d = [
      [tile(0, 0), tile(1, 0)],
      [tile(0, 1), tile(1, 1)],
    ];

    expect(collectBorderStrokes(tiles2d, TILE_SIZE_PX, context())).toHaveLength(0);
  });
});
