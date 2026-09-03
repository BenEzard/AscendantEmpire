// Invariants for the faction palette. The point of DistinctColors is a measurable claim — "no two of
// these can be mistaken for one another" — and a claim that nothing checks is one an innocent-looking
// edit silently breaks. So this re-derives the separation rather than trusting the comment.
//
// It also catches the palette's one silent failure mode: the colours are looked up in TailwindColors
// by name, so a renamed swatch would quietly shrink a band rather than raise anything.

import { DistinctColors } from "../distinctColors";
import { TailwindColors } from "../tailwindColors";

/** The floor the palette was selected against, in OKLab units. See the note in distinctColors.ts. */
const MINIMUM_SEPARATION = 0.12;

/** One sRGB channel (0-255) to linear-light. */
function srgbToLinear(channel: number): number {
  const normalised = channel / 255;
  return normalised <= 0.04045 ? normalised / 12.92 : Math.pow((normalised + 0.055) / 1.055, 2.4);
}

/**
 * Converts an "#rrggbb" string to OKLab.
 *
 * Tailwind 4 generates its ramps in OKLCH, so OKLab is the space the palette's separation was chosen in — and the one
 * where a numeric distance corresponds to how different two colours actually look.
 */
function toOklab(hex: string): [number, number, number] {
  const r = srgbToLinear(parseInt(hex.slice(1, 3), 16));
  const g = srgbToLinear(parseInt(hex.slice(3, 5), 16));
  const b = srgbToLinear(parseInt(hex.slice(5, 7), 16));

  const l = Math.cbrt(0.4122214708 * r + 0.5363325363 * g + 0.0514459929 * b);
  const m = Math.cbrt(0.2119034982 * r + 0.6806995451 * g + 0.1073969566 * b);
  const s = Math.cbrt(0.0883024619 * r + 0.2817188376 * g + 0.6299787005 * b);

  return [
    0.2104542553 * l + 0.793617785 * m - 0.0040720468 * s,
    1.9779984951 * l - 2.428592205 * m + 0.4505937099 * s,
    0.0259040371 * l + 0.7827717662 * m - 0.808675766 * s,
  ];
}

function oklabDistance(first: string, second: string): number {
  const [l1, a1, b1] = toOklab(first);
  const [l2, a2, b2] = toOklab(second);
  return Math.hypot(l1 - l2, a1 - a2, b1 - b2);
}

describe("the faction palette", () => {
  it("resolves every name, so a renamed Tailwind swatch cannot quietly shrink a band", () => {
    // resolve() drops names it cannot find rather than throwing at module load, which would take the
    // whole page down. These counts are what turn that into a visible failure instead.
    expect(DistinctColors.Light).toHaveLength(DistinctColors.BandSize);
    expect(DistinctColors.Deep).toHaveLength(DistinctColors.BandSize);
    expect(DistinctColors.AllColors).toHaveLength(DistinctColors.BandSize * 2);
  });

  it("takes its values from TailwindColors rather than restating them", () => {
    const tailwindByDescription = new Map(TailwindColors.AllColors.map((color) => [color.description, color.value]));

    for (const color of DistinctColors.AllColors) {
      expect(tailwindByDescription.get(color.description)).toBe(color.value);
    }
  });

  it("offers no colour twice", () => {
    const values = DistinctColors.AllColors.map((color) => color.value);

    expect(new Set(values).size).toBe(values.length);
  });

  it("keeps every pair at least as far apart as red is from orange", () => {
    const colors = DistinctColors.AllColors;

    // Reported as the single closest pair rather than one assertion per pair: when this breaks you want
    // to be told which two colours collided, not just that something did.
    let closest = { distance: Number.POSITIVE_INFINITY, pair: "" };

    for (let i = 0; i < colors.length; i++) {
      for (let j = i + 1; j < colors.length; j++) {
        const distance = oklabDistance(colors[i].value, colors[j].value);
        if (distance < closest.distance) {
          closest = { distance, pair: `${colors[i].description} / ${colors[j].description}` };
        }
      }
    }

    // Asserted as an object so the failure diff names the offending pair and its distance, which a bare
    // toBeGreaterThanOrEqual on the number would not.
    expect({ closestPair: closest.pair, farEnoughApart: closest.distance >= MINIMUM_SEPARATION }).toEqual({
      closestPair: closest.pair,
      farEnoughApart: true,
    });
  });

  it("hands out a fresh palette array each call, so a caller can append its own without side effects", () => {
    const first = DistinctColors.palettes();
    first.push({ title: "Mine", colors: [] });

    expect(DistinctColors.palettes()).toHaveLength(2);
  });
});
