import { DEFAULT_REMAINDER_COLOR, layOutCapacityBar, type CapacityBarBand } from "../capacityBarLayout";
import type { Color } from "../colorpicker/colorTypes";

// The layout is where the bar's one tricky rule lives: `unitWidth` is in the caller's own units, and
// whether there is a remainder at all depends on `totalUnits` being supplied. These pin both readings,
// and the input a caller can realistically get wrong.

const BLUE: Color = { value: "#0d6efd", description: "blue-500" };
const RED: Color = { value: "#dc3545", description: "red-500" };
const GREY: Color = { value: "#6c757d", description: "grey-500" };

/** Widths are unrounded percentages, so a third of a bar is only ever equal to a third to within a bit. */
function expectProportions(bands: readonly CapacityBarBand[], expectedPercents: readonly number[]) {
  expect(bands).toHaveLength(expectedPercents.length);
  bands.forEach((band, index) => {
    expect(band.widthPercent).toBeCloseTo(expectedPercents[index], 10);
  });
}

describe("when no total is given", () => {
  it("makes the segments the whole, so a breakdown of 80 and 40 fills the bar without clipping the second stage", () => {
    const bands = layOutCapacityBar({
      segments: [
        { unitWidth: 80, tooltip: "Construction (80)", fillColor: BLUE },
        { unitWidth: 40, tooltip: "Shipping (40)", fillColor: RED },
      ],
      defaultFillColor: GREY,
    });

    expectProportions(bands, [200 / 3, 100 / 3]);
    expect(bands.some((band) => band.isRemainder)).toBe(false);
  });

  it("falls back to the default fill for a segment that names no colour", () => {
    const bands = layOutCapacityBar({
      segments: [{ unitWidth: 1, tooltip: "Anything" }],
      defaultFillColor: GREY,
    });

    expect(bands[0].color).toBe(GREY);
  });
});

describe("when a total is given", () => {
  it("draws the shortfall as a trailing remainder, which is what shows the space left in a force's cargo", () => {
    const bands = layOutCapacityBar({
      segments: [{ unitWidth: 40, tooltip: "Carrying 40kg", fillColor: BLUE }],
      defaultFillColor: GREY,
      totalUnits: 100,
      remainderColor: RED,
      remainderTooltip: "60kg free",
    });

    expect(bands).toEqual([
      { widthPercent: 40, color: BLUE, tooltip: "Carrying 40kg", isRemainder: false },
      { widthPercent: 60, color: RED, tooltip: "60kg free", isRemainder: true },
    ]);
  });

  it("gives a needed remainder a default grey and no tooltip when the caller supplied neither", () => {
    const bands = layOutCapacityBar({
      segments: [{ unitWidth: 25, tooltip: "A quarter", fillColor: BLUE }],
      defaultFillColor: GREY,
      totalUnits: 100,
    });

    expect(bands[1]).toEqual({ widthPercent: 75, color: DEFAULT_REMAINDER_COLOR, tooltip: undefined, isRemainder: true });
  });

  it("reads as full with proportions preserved when the segments overrun the total, rather than overflowing the track", () => {
    const bands = layOutCapacityBar({
      segments: [
        { unitWidth: 80, tooltip: "Eighty", fillColor: BLUE },
        { unitWidth: 40, tooltip: "Forty", fillColor: RED },
      ],
      defaultFillColor: GREY,
      totalUnits: 100,
      remainderColor: GREY,
      remainderTooltip: "Free",
    });

    expectProportions(bands, [200 / 3, 100 / 3]);
    expect(bands.some((band) => band.isRemainder)).toBe(false);
  });

  it.each([0, -50, Number.NaN, Number.POSITIVE_INFINITY])(
    "ignores a total of %p and makes the segments the whole, so nothing divides by it",
    (totalUnits) => {
      const bands = layOutCapacityBar({
        segments: [{ unitWidth: 3, tooltip: "Three", fillColor: BLUE }],
        defaultFillColor: GREY,
        totalUnits,
      });

      expect(bands).toEqual([{ widthPercent: 100, color: BLUE, tooltip: "Three", isRemainder: false }]);
    },
  );
});

describe("degenerate input", () => {
  it.each([-10, Number.NaN, Number.POSITIVE_INFINITY])("treats a unitWidth of %p as zero", (unitWidth) => {
    const bands = layOutCapacityBar({
      segments: [
        { unitWidth, tooltip: "Nonsense", fillColor: RED },
        { unitWidth: 10, tooltip: "Ten", fillColor: BLUE },
      ],
      defaultFillColor: GREY,
    });

    expect(bands).toEqual([{ widthPercent: 100, color: BLUE, tooltip: "Ten", isRemainder: false }]);
  });

  it("drops a zero-width segment, so it cannot catch a hover for a tooltip about nothing", () => {
    const bands = layOutCapacityBar({
      segments: [
        { unitWidth: 0, tooltip: "Nothing here", fillColor: RED },
        { unitWidth: 5, tooltip: "Five", fillColor: BLUE },
      ],
      defaultFillColor: GREY,
    });

    expect(bands.map((band) => band.tooltip)).toEqual(["Five"]);
  });

  it("is all remainder when there is a total but nothing in it yet", () => {
    const bands = layOutCapacityBar({
      segments: [],
      defaultFillColor: GREY,
      totalUnits: 100,
      remainderColor: RED,
      remainderTooltip: "Empty",
    });

    expect(bands).toEqual([{ widthPercent: 100, color: RED, tooltip: "Empty", isRemainder: true }]);
  });

  it("is an empty track when there is neither a total nor anything to draw", () => {
    expect(layOutCapacityBar({ segments: [], defaultFillColor: GREY })).toEqual([]);
  });

  it("sums to exactly 100 across many segments, so the bar has no sub-pixel gap at its right edge", () => {
    const segments = Array.from({ length: 7 }, (_unused, index) => ({ unitWidth: index + 1, tooltip: `Segment ${index}` }));

    const total = layOutCapacityBar({ segments, defaultFillColor: GREY }).reduce((sum, band) => sum + band.widthPercent, 0);

    expect(total).toBeCloseTo(100, 10);
  });
});
