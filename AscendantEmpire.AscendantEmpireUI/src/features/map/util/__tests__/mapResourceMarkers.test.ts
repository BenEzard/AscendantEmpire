import type { PlayerMapResource } from "@/features/map/api/playerMapResourceApi";
import { describeMapResources, groupMapResourcesByTile } from "../mapResourceMarkers";

const resource = (overrides: Partial<PlayerMapResource> = {}): PlayerMapResource => ({
  x: 3,
  y: 4,
  slot: 1,
  mapResourceTypeId: 7,
  code: "BERRY",
  displayName: "Berries",
  iconPath: "/resources/food/berries_40px.png",
  isObserved: true,
  estimatedQuantity: 1250,
  ...overrides,
});

describe("groupMapResourcesByTile", () => {
  it("keys each tile the way the rest of the grid does", () => {
    const byTile = groupMapResourcesByTile([resource(), resource({ x: 9, y: 0 })]);

    expect([...byTile.keys()]).toEqual(["3,4", "9,0"]);
  });

  /** Two resources on one tile are drawn in slot order, so neither shifts position between reads. */
  it("orders a tile's resources by slot", () => {
    const byTile = groupMapResourcesByTile([resource({ slot: 2, displayName: "Copper" }), resource({ slot: 1 })]);

    expect(byTile.get("3,4")!.map((marker) => marker.name)).toEqual(["Berries", "Copper"]);
  });

  /** The tile decides what to draw: a resource with no icon still has to reach it, for the hover title. */
  it("keeps a resource whose type has no icon", () => {
    const byTile = groupMapResourcesByTile([resource({ displayName: "Wood", iconPath: null })]);

    expect(byTile.get("3,4")).toEqual([{ slot: 1, name: "Wood", iconPath: null, estimatedQuantity: 1250 }]);
  });

  it("gives no entry for a tile with nothing on it", () => {
    expect(groupMapResourcesByTile([resource()]).get("0,0")).toBeUndefined();
  });
});

describe("describeMapResources", () => {
  /** Each figure is tilded: a quantity shown for a patch is always the player's fuzzed estimate of it, never the true amount. */
  it("names each resource with its estimate", () => {
    const markers = groupMapResourcesByTile([resource(), resource({ slot: 2, displayName: "Copper", estimatedQuantity: 340 })]).get("3,4")!;

    expect(describeMapResources(markers)).toBe("Berries (~1.2k), Copper (~340)");
  });

  /** A patch the player cannot see the size of is not a patch that has been stripped bare, so it is named without a number rather than with a 0. */
  it("names a resource with no estimate without a number", () => {
    const markers = groupMapResourcesByTile([resource({ estimatedQuantity: null })]).get("3,4")!;

    expect(describeMapResources(markers)).toBe("Berries");
  });
});
