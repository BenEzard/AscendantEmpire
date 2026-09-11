import { render } from "@testing-library/react";
import type { KnownForce } from "@/features/intelligence/api/intelligenceApi";
import KnownForceMarker from "../KnownForceMarker";

function knownForce(overrides: Partial<KnownForce> = {}): KnownForce {
  return {
    status: "Sighted",
    fidelity: "Detail",
    lastSeenLocation: { x: 4, y: 1 },
    lastSeenPlayerTurnId: 13,
    lastSeenRoundNumber: 2,
    lastSeenTurnSequence: 4,
    exitDirection: "West",
    stillVisible: false,
    unitTemplateId: 3,
    ownerPlayerId: 1,
    unitInstanceId: 99,
    healthyTroops: 45,
    ...overrides,
  };
}

const IMAGES = new Map([[3, "settler.png"]]);

describe("KnownForceMarker", () => {
  it("draws the unit's picture, fading with the rounds since it was seen", () => {
    const { container } = render(<KnownForceMarker knownForces={[knownForce()]} currentRoundNumber={2} unitImageSrcByUnitTemplateId={IMAGES} />);

    expect(container.querySelector(".known-force__image")?.getAttribute("src")).toBe("settler.png");
    expect(container.querySelector(".known-force__heading")).not.toBeNull();
  });

  /**
   * The marker can never show more than the sighting earned. With the template withheld there is no picture to choose, and guessing one would say what kind of
   * force it was.
   */
  it("draws a question mark when the kind of force was not made out", () => {
    const { container } = render(<KnownForceMarker knownForces={[knownForce({ unitTemplateId: null })]} currentRoundNumber={2} unitImageSrcByUnitTemplateId={IMAGES} />);

    expect(container.querySelector(".known-force__image")).toBeNull();
    expect(container.querySelector(".known-force__unknown")?.textContent).toBe("?");
  });

  /** A force watched dying is shown as destroyed, and has no heading, because it did not leave. */
  it("marks a destroyed force and gives it no heading", () => {
    const { container } = render(<KnownForceMarker knownForces={[knownForce({ status: "Destroyed" })]} currentRoundNumber={2} unitImageSrcByUnitTemplateId={IMAGES} />);

    expect(container.querySelector(".known-force--destroyed")).not.toBeNull();
    expect(container.querySelector(".known-force__destroyed-mark")).not.toBeNull();
    expect(container.querySelector(".known-force__heading")).toBeNull();
  });

  it("counts every force last seen on the tile but shows only the freshest", () => {
    const { container } = render(
      <KnownForceMarker
        knownForces={[knownForce({ unitTemplateId: null }), knownForce()]}
        currentRoundNumber={2}
        unitImageSrcByUnitTemplateId={IMAGES}
      />,
    );

    expect(container.querySelector(".known-force__count")?.textContent).toBe("2");
    expect(container.querySelector(".known-force__unknown")).not.toBeNull();
  });
});
