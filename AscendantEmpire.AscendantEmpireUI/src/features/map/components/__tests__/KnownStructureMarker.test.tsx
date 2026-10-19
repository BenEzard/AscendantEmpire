import { render } from "@testing-library/react";
import type { KnownStructure } from "@/features/intelligence/api/intelligenceApi";
import KnownStructureMarker from "../KnownStructureMarker";

/**
 * The structure catalogue as /structures/display gives it: id 1 is the watchtower, which the client has art for, and id 2 is a
 * kind it has none for.
 *
 * A remembered sighting carries only a structure id, so the marker comes back through this to find the code the artwork is
 * keyed on. Two lookups rather than one art table is the price of the id being all a sighting discloses.
 */
const STRUCTURE_CODE_BY_ID: ReadonlyMap<number, string> = new Map([
  [1, "wooden-watch-tower"],
  [2, "something-unknown-to-the-client"],
]);

/** Player 1's watchtower, seen in full detail and 40% built, unless a test says otherwise. */
function knownStructure(overrides: Partial<KnownStructure> = {}): KnownStructure {
  return {
    status: "Standing",
    fidelity: "Detail",
    location: { x: 4, y: 1 },
    lastSeenPlayerTurnId: 13,
    lastSeenRoundNumber: 2,
    lastSeenTurnSequence: 4,
    stillVisible: false,
    structureId: 1,
    ownerPlayerId: 1,
    structureInstanceId: 60,
    completionPercent: 40,
    ...overrides,
  };
}

describe("KnownStructureMarker", () => {
  it("draws the structure's picture with how far it was built", () => {
    const { container } = render(
      <KnownStructureMarker knownStructures={[knownStructure()]} currentRoundNumber={2} structureCodeByStructureId={STRUCTURE_CODE_BY_ID} />,
    );

    expect(container.querySelector(".known-structure__image")).not.toBeNull();
    expect(container.querySelector<HTMLElement>(".known-structure__completion-fill")?.style.height).toBe("40%");
  });

  /**
   * The marker can never show more than the sighting earned. With the kind withheld there is no picture to choose, and with the progress withheld there is no
   * bar to draw.
   */
  it("draws a question mark and no progress when neither was made out", () => {
    const { container } = render(
      <KnownStructureMarker
        knownStructures={[knownStructure({ structureId: null, completionPercent: null })]}
        currentRoundNumber={2}
        structureCodeByStructureId={STRUCTURE_CODE_BY_ID}
      />,
    );

    expect(container.querySelector(".known-structure__image")).toBeNull();
    expect(container.querySelector(".known-structure__unknown")?.textContent).toBe("?");
    expect(container.querySelector(".known-structure__completion-bar")).toBeNull();
  });

  /** A kind that was made out but has no art yet is a plain building, not a question mark that would claim the kind is unknown. */
  it("draws a plain building for a known kind the client has no art for", () => {
    const { container } = render(
      <KnownStructureMarker knownStructures={[knownStructure({ structureId: 2 })]} currentRoundNumber={2} structureCodeByStructureId={STRUCTURE_CODE_BY_ID} />,
    );

    expect(container.querySelector(".known-structure__unknown")).toBeNull();
    expect(container.querySelector(".known-structure__generic")).not.toBeNull();
  });

  /**
   * The catalogue is fetched separately from the sightings, so the marker can render before it lands. A kind that was plainly
   * made out must not then read as unidentified — it falls back to the plain building until the codes arrive.
   */
  it("draws a plain building rather than a question mark while the structure catalogue is still loading", () => {
    const { container } = render(<KnownStructureMarker knownStructures={[knownStructure()]} currentRoundNumber={2} />);

    expect(container.querySelector(".known-structure__unknown")).toBeNull();
    expect(container.querySelector(".known-structure__generic")).not.toBeNull();
  });

  /** A structure watched being razed is shown as razed, and is no longer being built. */
  it("marks a razed structure and draws no progress for it", () => {
    const { container } = render(
      <KnownStructureMarker
        knownStructures={[knownStructure({ status: "Razed" })]}
        currentRoundNumber={2}
        structureCodeByStructureId={STRUCTURE_CODE_BY_ID}
      />,
    );

    expect(container.querySelector(".known-structure--razed")).not.toBeNull();
    expect(container.querySelector(".known-structure__razed-mark")).not.toBeNull();
    expect(container.querySelector(".known-structure__completion-bar")).toBeNull();
  });

  it("counts every structure last seen on the tile but shows only the freshest", () => {
    const { container } = render(
      <KnownStructureMarker
        knownStructures={[knownStructure({ structureId: null }), knownStructure()]}
        currentRoundNumber={2}
        structureCodeByStructureId={STRUCTURE_CODE_BY_ID}
      />,
    );

    expect(container.querySelector(".known-structure__count")?.textContent).toBe("2");
    expect(container.querySelector(".known-structure__unknown")).not.toBeNull();
  });
});
