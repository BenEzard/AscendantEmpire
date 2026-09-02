import { directionBetween } from "../mapDirections";

describe("directionBetween", () => {
  it("names the step to each of the eight neighbours", () => {
    const origin = { x: 5, y: 4 };

    // Laid out as they sit around the origin. North is decreasing Y, which draws upward — getting that backwards
    // would send a held-tile order to the opposite side of the force.
    expect(directionBetween(origin, { x: 4, y: 3 })).toBe("NorthWest");
    expect(directionBetween(origin, { x: 5, y: 3 })).toBe("North");
    expect(directionBetween(origin, { x: 6, y: 3 })).toBe("NorthEast");
    expect(directionBetween(origin, { x: 4, y: 4 })).toBe("West");
    expect(directionBetween(origin, { x: 6, y: 4 })).toBe("East");
    expect(directionBetween(origin, { x: 4, y: 5 })).toBe("SouthWest");
    expect(directionBetween(origin, { x: 5, y: 5 })).toBe("South");
    expect(directionBetween(origin, { x: 6, y: 5 })).toBe("SouthEast");
  });

  it("gives no direction for the tile the force is already on", () => {
    expect(directionBetween({ x: 5, y: 4 }, { x: 5, y: 4 })).toBeNull();
  });

  it("gives no direction for a tile more than one step away", () => {
    expect(directionBetween({ x: 5, y: 4 }, { x: 7, y: 4 })).toBeNull();
    expect(directionBetween({ x: 5, y: 4 }, { x: 5, y: 8 })).toBeNull();
    expect(directionBetween({ x: 5, y: 4 }, { x: 0, y: 0 })).toBeNull();
  });

  it("treats a tile across a wrap seam as too far, rather than guessing", () => {
    // The client isn't told whether a map wraps, so the far end of a row is not assumed to be the neighbour it
    // would be on a wrapping map. The numpad path is unaffected: it sends a direction and the server wraps.
    expect(directionBetween({ x: 0, y: 4 }, { x: 39, y: 4 })).toBeNull();
  });
});
