import { render } from "@testing-library/react";
import BorderStrokeLayer, { type BorderStroke } from "../BorderStrokeLayer";

const ROME_COLOUR = "#b91c1c";
const CARTHAGE_COLOUR = "#1d4ed8";

/** One horizontal segment, which is all any of these need. */
function stroke(overrides: Partial<BorderStroke> = {}): BorderStroke {
  return { key: "0,0,north", x1: 0, y1: 0, x2: 50, y2: 0, colours: [ROME_COLOUR], isDashed: false, ...overrides };
}

function renderLayer(strokes: BorderStroke[]) {
  const { container } = render(<BorderStrokeLayer strokes={strokes} widthPx={100} heightPx={100} />);

  return {
    /** The drawn colour lines, excluding the casings underneath them. */
    lines: [...container.querySelectorAll('[data-testid="border-strokes"] line')],
    casings: [...container.querySelectorAll('[data-testid="border-casings"] line')],
    container,
  };
}

/**
 * The layer decides how a border *looks*: solid for ownership, dashed for a claim, and — where several players claim one tile —
 * their colours taking turns along the dashes. Which sides carry a border at all is `ownershipBands`' business, not this.
 */
describe("BorderStrokeLayer", () => {
  it("draws an owned border as one solid line in the owner's colour", () => {
    const { lines } = renderLayer([stroke()]);

    expect(lines).toHaveLength(1);
    expect(lines[0].getAttribute("stroke")).toBe(ROME_COLOUR);
    // No pattern at all, which is what distinguishes real control from an assertion.
    expect(lines[0].getAttribute("stroke-dasharray")).toBeNull();
    expect(lines[0].getAttribute("data-border-kind")).toBe("owned");
  });

  /**
   * A single claimant's border is dashed with transparent gaps, so the terrain shows through between the dashes — the visual
   * difference between "I assert this" and "I hold this".
   */
  it("draws a single claimant's border as one dashed line with equal gaps", () => {
    const { lines } = renderLayer([stroke({ colours: [ROME_COLOUR], isDashed: true })]);

    expect(lines).toHaveLength(1);
    expect(lines[0].getAttribute("stroke")).toBe(ROME_COLOUR);
    // On for one dash, off for one dash: the gap is as long as the mark, and nothing is painted in it.
    expect(lines[0].getAttribute("stroke-dasharray")).toBe("6 6");
    expect(lines[0].getAttribute("data-border-kind")).toBe("claim");
  });

  /**
   * Two claimants means two dashed lines over the same segment, the second offset by a whole dash-and-gap so its marks land in
   * the first's gaps. The result reads colour, gap, colour, gap — still a dashed line, now naming both claimants.
   */
  it("interleaves two claimants' colours along the dashes, leaving the gaps empty", () => {
    const { lines } = renderLayer([stroke({ colours: [ROME_COLOUR, CARTHAGE_COLOUR], isDashed: true })]);

    expect(lines).toHaveLength(2);

    expect(lines[0].getAttribute("stroke")).toBe(ROME_COLOUR);
    expect(lines[1].getAttribute("stroke")).toBe(CARTHAGE_COLOUR);

    // A cycle of two dashes and two gaps: each colour is on for 6 and off for 18, so together they fill two of the four slots.
    expect(lines[0].getAttribute("stroke-dasharray")).toBe("6 18");
    expect(lines[1].getAttribute("stroke-dasharray")).toBe("6 18");

    // The second starts a whole dash-and-gap further along, which is what puts it in the first's gap rather than on top of it.
    expect(lines[0].getAttribute("stroke-dashoffset")).toBe("0");
    expect(lines[1].getAttribute("stroke-dashoffset")).toBe("-12");
  });

  it("extends the same interleaving to three claimants", () => {
    const { lines } = renderLayer([stroke({ colours: [ROME_COLOUR, CARTHAGE_COLOUR, "#16a34a"], isDashed: true })]);

    expect(lines).toHaveLength(3);
    // Three dashes and three gaps per cycle: on for 6, off for 30.
    expect(lines.map((line) => line.getAttribute("stroke-dasharray"))).toEqual(["6 30", "6 30", "6 30"]);
    expect(lines.map((line) => line.getAttribute("stroke-dashoffset"))).toEqual(["0", "-12", "-24"]);
  });

  /**
   * Every stroke is cased - a darker line drawn wider underneath it - so a faction colour keeps the same weight whether it
   * crosses pale sand or near-black water. Without it the same indigo reads far darker against one biome than another.
   */
  it("cases every stroke, including each claimant's", () => {
    const { lines, casings } = renderLayer([stroke({ colours: [ROME_COLOUR, CARTHAGE_COLOUR], isDashed: true })]);

    expect(lines).toHaveLength(2);
    expect(casings).toHaveLength(2);
  });

  it("draws the casing wider than the stroke it backs", () => {
    const { lines, casings } = renderLayer([stroke()]);

    const strokeWidth = Number(lines[0].getAttribute("stroke-width"));
    const casingWidth = Number(casings[0].getAttribute("stroke-width"));

    expect(casingWidth).toBeGreaterThan(strokeWidth);
  });

  /**
   * The casing has to carry the same dash pattern as the stroke it backs. A solid casing under a claim would paint the gaps the
   * dashes deliberately leave open, and those gaps are what let the terrain through.
   */
  it("gives each casing the dash pattern of its own run, so a claim's gaps stay transparent", () => {
    const { casings } = renderLayer([stroke({ colours: [ROME_COLOUR, CARTHAGE_COLOUR], isDashed: true })]);

    expect(casings.map((casing) => casing.getAttribute("stroke-dasharray"))).toEqual(["6 18", "6 18"]);
    expect(casings.map((casing) => casing.getAttribute("stroke-dashoffset"))).toEqual(["0", "-12"]);
  });

  /** A solid line has no gaps to keep clear, so its casing is solid too. */
  it("leaves a solid border's casing unpatterned", () => {
    const { casings } = renderLayer([stroke()]);

    expect(casings).toHaveLength(1);
    expect(casings[0].getAttribute("stroke-dasharray")).toBeNull();
  });

  /** A solid border belongs to one player, so a second colour would have nowhere to go along an unbroken line. */
  it("draws only the first colour of a solid border", () => {
    const { lines } = renderLayer([stroke({ colours: [ROME_COLOUR, CARTHAGE_COLOUR], isDashed: false })]);

    expect(lines).toHaveLength(1);
    expect(lines[0].getAttribute("stroke")).toBe(ROME_COLOUR);
  });

  it("renders nothing at all when there is nothing to draw", () => {
    const { container } = renderLayer([]);

    expect(container.querySelector("svg")).toBeNull();
  });

  /** A stroke with no colours cannot be drawn, and must not leave an empty line behind either. */
  it("skips a stroke that carries no colours", () => {
    const { container } = renderLayer([stroke({ colours: [] })]);

    expect(container.querySelector("svg")).toBeNull();
  });
});
