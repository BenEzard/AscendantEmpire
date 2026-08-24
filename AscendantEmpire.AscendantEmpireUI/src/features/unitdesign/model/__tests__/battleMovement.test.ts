import { describe, expect, it } from "vitest";

import { MINIMUM_BATTLE_MOVEMENT_POINTS_RAW, calculateBattleMovementPoints } from "../battleMovement";

// Every value here is raw Scale-1,000 fixed-point (1,000 raw = 1.0).
//
// These are the agreed UD-52 calibration numbers, and they are deliberately the same cases the server pins
// in BattleMovementCalculatorTests.cs. The two implementations only stay honest if they are held to the same
// table, so a change here that isn't mirrored there (or vice versa) should be treated as a bug.
const HUMAN_BODY_POWER = 7_000;
const SPEAR_WEIGHT = 1_000; // A sword weighs the same as a spear.
const KNIFE_WEIGHT = 500;
const ARMOUR_WEIGHT = 500;
const SHIELD_WEIGHT = 500;

describe("calculateBattleMovementPoints", () => {
  it("leaves 6 tiles for a man carrying only a sword or a spear", () => {
    expect(calculateBattleMovementPoints(HUMAN_BODY_POWER, SPEAR_WEIGHT)).toBe(6_000);
  });

  it("leaves 6.5 tiles for a man carrying only a knife", () => {
    // The half-tile case: the fixed-point scale has to carry it exactly, with no rounding drift.
    expect(calculateBattleMovementPoints(HUMAN_BODY_POWER, KNIFE_WEIGHT)).toBe(6_500);
  });

  it("leaves 5 tiles for a man carrying armour, a spear and a shield", () => {
    const carriedWeight = ARMOUR_WEIGHT + SPEAR_WEIGHT + SHIELD_WEIGHT;

    expect(carriedWeight).toBe(2_000);
    expect(calculateBattleMovementPoints(HUMAN_BODY_POWER, carriedWeight)).toBe(5_000);
  });

  it("leaves the full power when nothing is carried", () => {
    expect(calculateBattleMovementPoints(HUMAN_BODY_POWER, 0)).toBe(HUMAN_BODY_POWER);
  });

  it("floors an over-loaded design at zero rather than going negative", () => {
    expect(calculateBattleMovementPoints(HUMAN_BODY_POWER, HUMAN_BODY_POWER)).toBe(0);
    expect(calculateBattleMovementPoints(HUMAN_BODY_POWER, HUMAN_BODY_POWER * 2)).toBe(MINIMUM_BATTLE_MOVEMENT_POINTS_RAW);
  });
});
