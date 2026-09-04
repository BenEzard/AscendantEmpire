import { describe, expect, it } from "vitest";

import { aggregateDesign } from "../unitDesign";
import { createEmptyBody, type UnitComponent } from "../../types/UnitComponent";

// Raw Scale-1,000 values throughout (1,000 raw = 1.0), matching the UD-52 calibration.
const HUMAN_BODY_POWER = 7_000;
const HUMAN_BODY_WEIGHT = 1_000;
const SPEAR_WEIGHT = 1_000;
const KNIFE_WEIGHT = 500;
const ARMOUR_WEIGHT = 500;
const SHIELD_WEIGHT = 500;

/** A minimal non-body component of the given weight — enough for the aggregate, which only reads weight here. */
function carriedComponent(code: string, weight: number): UnitComponent {
  return {
    id: null,
    code,
    displayName: code,
    description: null,
    designerImagePath: null,
    componentType: "Equipment",
    weight,
    sight: null,
    detection: null,
    visibility: null,
    body: null,
    weapon: null,
    armour: null,
    tagCodes: [],
    supersededBy: [],
  };
}

/** The human body: weight 1.0 and power 7.0, the anchor the whole calibration is built on. */
function humanBody(): UnitComponent {
  return {
    ...carriedComponent("human", HUMAN_BODY_WEIGHT),
    componentType: "Body",
    body: { ...createEmptyBody(), power: HUMAN_BODY_POWER },
  };
}

describe("aggregateDesign — battle movement", () => {
  it("gives a man carrying only a spear 6 tiles", () => {
    const { summary } = aggregateDesign(humanBody(), [carriedComponent("spear", SPEAR_WEIGHT)]);

    expect(summary.battleMovementPoints).toBe(6_000);
  });

  it("gives a man carrying only a knife 6.5 tiles", () => {
    const { summary } = aggregateDesign(humanBody(), [carriedComponent("knife", KNIFE_WEIGHT)]);

    expect(summary.battleMovementPoints).toBe(6_500);
  });

  it("gives a man carrying armour, a spear and a shield 5 tiles", () => {
    const { summary } = aggregateDesign(humanBody(), [
      carriedComponent("armour", ARMOUR_WEIGHT),
      carriedComponent("spear", SPEAR_WEIGHT),
      carriedComponent("shield", SHIELD_WEIGHT),
    ]);

    expect(summary.battleMovementPoints).toBe(5_000);
  });

  it("does not charge the design for the body's own weight", () => {
    // The body weighs 1.0 and is included in the Weight row, but movement is the full 7.0 power: the two
    // numbers disagreeing is the point of the formula, not a bug.
    const { summary } = aggregateDesign(humanBody(), []);

    expect(summary.weight).toBe(HUMAN_BODY_WEIGHT);
    expect(summary.battleMovementPoints).toBe(HUMAN_BODY_POWER);
  });

  it("counts the weight of every fitted component, and floors an over-loaded design at zero", () => {
    const anvil = carriedComponent("anvil", HUMAN_BODY_POWER);
    const { summary } = aggregateDesign(humanBody(), [anvil, carriedComponent("spear", SPEAR_WEIGHT)]);

    expect(summary.weight).toBe(HUMAN_BODY_WEIGHT + HUMAN_BODY_POWER + SPEAR_WEIGHT);
    expect(summary.battleMovementPoints).toBe(0);
  });

  it("reports no movement when no base is chosen yet", () => {
    const { summary } = aggregateDesign(null, []);

    expect(summary.battleMovementPoints).toBe(0);
  });
});

describe("aggregateDesign — sight", () => {
  /** A component carrying a sight value, so the sum can be told apart from the base's own contribution. */
  function seeingComponent(code: string, sight: number): UnitComponent {
    return { ...carriedComponent(code, 0), sight };
  }

  it("sums sight across the base and everything fitted to it", () => {
    const base = { ...humanBody(), sight: 2_000 };
    const { summary } = aggregateDesign(base, [seeingComponent("spyglass", 1_500), seeingComponent("scout_training", 500)]);

    expect(summary.sight).toBe(4_000);
  });

  it("treats a null sight as no contribution rather than a default", () => {
    // Sight is nullable end to end: an un-authored component adds nothing, so a design built entirely from
    // them totals zero and sees only the tile it occupies.
    const { summary } = aggregateDesign(humanBody(), [carriedComponent("spear", SPEAR_WEIGHT)]);

    expect(summary.sight).toBe(0);
  });
});
