import { describeRiverStep } from "../describeRiverStep";

/** The four fields of a step's response that the wording is built from; everything else about the step is irrelevant here. */
function step(overrides: Partial<Parameters<typeof describeRiverStep>[0]> = {}) {
  return { riverCrossed: "None" as const, riverAlong: "None" as const, stepCost: 1, riverMovementCost: 0, ...overrides };
}

/**
 * A river changes what a step costs without changing anything visible on the tile the force moved onto, so the player is told about
 * it. Every figure in these sentences comes off the response: the movement rules live on the server and differ by movement domain,
 * so the client reports what was paid rather than working it out.
 */
describe("describeRiverStep", () => {
  it("says nothing about a step that went nowhere near a river", () => {
    expect(describeRiverStep(step())).toBeNull();
  });

  it("names the river crossed and what the crossing added to the step", () => {
    const message = describeRiverStep(step({ riverCrossed: "Medium", stepCost: 2, riverMovementCost: 1 }));

    expect(message).toBe("Crossed a medium river. The step cost 2 MP, 1 more than the ground alone.");
  });

  it("names the river followed and what following it saved", () => {
    const message = describeRiverStep(step({ riverAlong: "Large", stepCost: 0.5, riverMovementCost: -0.5 }));

    expect(message).toBe("Followed a large river. The step cost 0.5 MP, 0.5 less than the ground alone.");
  });

  it("names both rivers when a step crossed one and followed another, with a single net adjustment", () => {
    const message = describeRiverStep(step({ riverCrossed: "Medium", riverAlong: "Large", stepCost: 1.5, riverMovementCost: 0.5 }));

    expect(message).toBe("Crossed a medium river and followed a large river. The step cost 1.5 MP, 0.5 more than the ground alone.");
  });

  /** An air force crosses rivers without paying for them, so the crossing is still reported but no adjustment is claimed. */
  it("reports a crossing that cost nothing without claiming the river changed the price", () => {
    const message = describeRiverStep(step({ riverCrossed: "Large", stepCost: 1, riverMovementCost: 0 }));

    expect(message).toBe("Crossed a large river. The step cost 1 MP.");
  });
});
