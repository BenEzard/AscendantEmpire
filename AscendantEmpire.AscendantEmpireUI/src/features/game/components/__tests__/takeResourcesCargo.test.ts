import {
  buildPlan,
  createCargoState,
  hasChanges,
  moveCargo,
  reduceCargo,
  type AssetStack,
  type TakeResourcesForce,
  type TileAssetStack,
} from "../takeResourcesCargo";

const BERRIES: AssetStack = { assetId: 1, displayName: "Berries", iconPath: "", quantity: 0 };
const FISH: AssetStack = { assetId: 2, displayName: "Fish", iconPath: "", quantity: 0 };

function tile(quantity: number, isEstimate = true): TileAssetStack[] {
  return [{ ...BERRIES, quantity, isEstimate }];
}

function force(overrides: Partial<TakeResourcesForce> = {}): TakeResourcesForce {
  return {
    forceId: 1,
    displayName: "Force 1",
    imageSrc: undefined,
    healthyIndividuals: 40,
    injuredIndividuals: 0,
    initialStrength: 40,
    movementRemaining: 3,
    movementPoints: 3,
    turnRemaining: 1,
    capacity: 200,
    carrying: [],
    ...overrides,
  };
}

const AVAILABLE = { kind: "available" } as const;
const atForce = (forceId: number) => ({ kind: "force", forceId }) as const;

describe("moveCargo", () => {
  it("takes only what the Force has room for and leaves the rest on the tile", () => {
    const forces = [force()];
    const state = moveCargo(createCargoState(tile(7000), forces), forces, AVAILABLE, atForce(1), BERRIES.assetId);

    expect(state.forces[0].carrying).toEqual([{ ...BERRIES, quantity: 200 }]);
    expect(state.available[0].quantity).toBe(6800);
    expect(state.available[0].isEstimate).toBe(true);
  });

  it("moves a whole stack between Forces when the receiver has room for it", () => {
    const forces = [force({ carrying: [{ ...BERRIES, quantity: 50 }] }), force({ forceId: 2 })];
    const state = moveCargo(createCargoState([], forces), forces, atForce(1), atForce(2), BERRIES.assetId);

    expect(state.forces[0].carrying).toEqual([]);
    expect(state.forces[1].carrying[0].quantity).toBe(50);
  });

  it("moves only what fits when the receiving Force is nearly full", () => {
    const forces = [force({ carrying: [{ ...BERRIES, quantity: 50 }] }), force({ forceId: 2, capacity: 30 })];
    const state = moveCargo(createCargoState([], forces), forces, atForce(1), atForce(2), BERRIES.assetId);

    expect(state.forces[0].carrying[0].quantity).toBe(20);
    expect(state.forces[1].carrying[0].quantity).toBe(30);
  });

  it("refuses a pickup by a Force without its whole turn", () => {
    const forces = [force({ turnRemaining: 0.5 })];
    const initial = createCargoState(tile(100), forces);

    expect(moveCargo(initial, forces, AVAILABLE, atForce(1), BERRIES.assetId)).toBe(initial);
  });

  it("lets a Force without a turn drop cargo, adding a new tile entry that is not an estimate", () => {
    const forces = [force({ turnRemaining: 0, carrying: [{ ...FISH, quantity: 10 }] })];
    const state = moveCargo(createCargoState(tile(100), forces), forces, atForce(1), AVAILABLE, FISH.assetId);

    expect(state.forces[0].carrying).toEqual([]);
    expect(state.available).toContainEqual({ ...FISH, quantity: 10, isEstimate: false });
  });

  it("lets a Force without its whole turn take back what it put down", () => {
    const forces = [force({ turnRemaining: 0.5, carrying: [{ ...BERRIES, quantity: 10 }] })];
    const dropped = moveCargo(createCargoState(tile(100), forces), forces, atForce(1), AVAILABLE, BERRIES.assetId);
    const retaken = moveCargo(dropped, forces, AVAILABLE, atForce(1), BERRIES.assetId);

    expect(retaken.forces[0].carrying[0].quantity).toBe(10);
    expect(retaken.available[0].quantity).toBe(100);
  });
});

describe("reduceCargo", () => {
  it("puts the difference back on the tile", () => {
    const forces = [force()];
    const taken = moveCargo(createCargoState(tile(7000), forces), forces, AVAILABLE, atForce(1), BERRIES.assetId);
    const state = reduceCargo(taken, 1, BERRIES.assetId, 150);

    expect(state.forces[0].carrying[0].quantity).toBe(150);
    expect(state.available[0].quantity).toBe(6850);
  });

  it("ignores a figure above what is carried", () => {
    const forces = [force({ carrying: [{ ...BERRIES, quantity: 10 }] })];
    const initial = createCargoState([], forces);

    expect(reduceCargo(initial, 1, BERRIES.assetId, 50)).toBe(initial);
  });
});

describe("buildPlan", () => {
  it("charges a turn to the Force that gains cargo but not to the one that gives it", () => {
    const forces = [force({ carrying: [{ ...BERRIES, quantity: 50 }] }), force({ forceId: 2 })];
    const state = moveCargo(createCargoState([], forces), forces, atForce(1), atForce(2), BERRIES.assetId);
    const plan = buildPlan(state, forces);

    expect(plan.forces.map((entry) => entry.spendsTurn)).toEqual([false, true]);
  });

  it("charges nothing when cargo is moved away and back again", () => {
    const forces = [force({ carrying: [{ ...BERRIES, quantity: 50 }] }), force({ forceId: 2 })];
    const there = moveCargo(createCargoState([], forces), forces, atForce(1), atForce(2), BERRIES.assetId);
    const back = moveCargo(there, forces, atForce(2), atForce(1), BERRIES.assetId);

    expect(buildPlan(back, forces).forces.every((entry) => !entry.spendsTurn)).toBe(true);
    expect(hasChanges(back, forces)).toBe(false);
  });
});
